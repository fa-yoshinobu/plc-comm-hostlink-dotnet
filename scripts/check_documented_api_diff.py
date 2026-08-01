#!/usr/bin/env python3
"""Compare a candidate assembly with an immutable stable-package API baseline."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import subprocess
import sys
import tempfile
import urllib.request
import zipfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any


ALLOWED_CATEGORIES = {
    "documented-contract",
    "undocumented-public",
    "additive",
    "generated-or-noncontract",
}

CSHARP_INSPECTOR = r'''
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

static string LocalTypeId(Type type)
{
    string name = type.Name;
    int tick = name.IndexOf('`');
    if (tick >= 0) name = name[..tick];
    int declaringArguments = type.DeclaringType?.GetGenericArguments().Length ?? 0;
    int ownArguments = type.IsGenericType ? type.GetGenericArguments().Length - declaringArguments : 0;
    return ownArguments == 0 ? name : name + "`" + ownArguments;
}

static string DeclaringTypeId(Type type)
{
    if (type.DeclaringType is not null)
        return DeclaringTypeId(type.DeclaringType) + "." + LocalTypeId(type);
    string prefix = string.IsNullOrEmpty(type.Namespace) ? "" : type.Namespace + ".";
    return prefix + LocalTypeId(type);
}

static string RemoveGenericArities(string value)
{
    var result = new System.Text.StringBuilder();
    for (int index = 0; index < value.Length; index++)
    {
        if (value[index] != '`')
        {
            result.Append(value[index]);
            continue;
        }
        while (index + 1 < value.Length && char.IsDigit(value[index + 1])) index++;
    }
    return result.ToString();
}

static string TypeIdName(Type type)
{
    if (type.IsByRef) return TypeIdName(type.GetElementType()!) + "@";
    if (type.IsPointer) return TypeIdName(type.GetElementType()!) + "*";
    if (type.IsArray) return TypeIdName(type.GetElementType()!) + "[]";
    if (type.IsGenericParameter)
        return (type.DeclaringMethod is null ? "`" : "``") + type.GenericParameterPosition;
    if (type.IsGenericType)
    {
        var definition = type.GetGenericTypeDefinition();
        var name = RemoveGenericArities(DeclaringTypeId(definition));
        return name + "{" + string.Join(",", type.GetGenericArguments().Select(TypeIdName)) + "}";
    }
    return DeclaringTypeId(type);
}

static string GenericConstraints(IEnumerable<Type> parameters) => string.Join(";", parameters.Select(parameter =>
{
    var attributes = parameter.GenericParameterAttributes;
    var parts = new List<string>();
    if ((attributes & GenericParameterAttributes.Covariant) != 0) parts.Add("out");
    if ((attributes & GenericParameterAttributes.Contravariant) != 0) parts.Add("in");
    if ((attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0) parts.Add("class");
    if ((attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0) parts.Add("struct");
    if ((attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0) parts.Add("new()");
    parts.AddRange(parameter.GetGenericParameterConstraints().Select(TypeIdName).OrderBy(value => value));
    return parameter.Name + ":" + string.Join("&", parts);
}));

static string DefaultValue(ParameterInfo parameter)
{
    if (!parameter.HasDefaultValue) return "required";
    object? value = parameter.DefaultValue;
    if (value is null)
        return parameter.ParameterType.IsValueType && Nullable.GetUnderlyingType(parameter.ParameterType) is null
            ? "default"
            : "null";
    if (value is Missing) return "missing";
    if (value is string text) return JsonSerializer.Serialize(text);
    if (value is char character) return ((int)character).ToString(CultureInfo.InvariantCulture);
    if (value is bool flag) return flag ? "true" : "false";
    if (value.GetType().IsEnum)
        return Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
    return Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.ToString() ?? "";
}

static string ParameterSignature(ParameterInfo parameter)
{
    string modifier = parameter.IsOut ? "out" : parameter.ParameterType.IsByRef ? "ref" : "value";
    var nullability = new NullabilityInfoContext().Create(parameter);
    return string.Join("|", modifier, TypeIdName(parameter.ParameterType), parameter.Name, DefaultValue(parameter),
        "read=" + nullability.ReadState, "write=" + nullability.WriteState);
}

static string MethodId(MethodInfo method)
{
    string generic = method.IsGenericMethodDefinition ? "``" + method.GetGenericArguments().Length : "";
    string parameters = string.Join(",", method.GetParameters().Select(parameter => TypeIdName(parameter.ParameterType)));
    string conversionReturn = method.Name is "op_Implicit" or "op_Explicit" ? "~" + TypeIdName(method.ReturnType) : "";
    return $"M:{DeclaringTypeId(method.DeclaringType!)}.{method.Name}{generic}({parameters}){conversionReturn}";
}

static string ConstructorId(ConstructorInfo constructor)
{
    string parameters = string.Join(",", constructor.GetParameters().Select(parameter => TypeIdName(parameter.ParameterType)));
    return $"M:{DeclaringTypeId(constructor.DeclaringType!)}.#ctor({parameters})";
}

static bool IsExternallyAccessible(MethodBase method) =>
    method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;

static string MethodAccess(MethodBase method) => method.IsPublic ? "public" : method.IsFamily ? "protected" : "protected-internal";

static string FieldAccess(FieldInfo field) => field.IsPublic ? "public" : field.IsFamily ? "protected" : "protected-internal";

static string TypeAccess(Type type) => type.IsPublic || type.IsNestedPublic
    ? "public"
    : type.IsNestedFamily ? "protected" : "protected-internal";

static bool IsExternallyVisibleType(Type type)
{
    bool accessible = type.IsPublic || type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamORAssem;
    return accessible && (type.DeclaringType is null || IsExternallyVisibleType(type.DeclaringType));
}

static IEnumerable<Type> DirectInterfaces(Type type)
{
    var inherited = new HashSet<Type>(type.BaseType?.GetInterfaces() ?? []);
    foreach (Type implemented in type.GetInterfaces())
        foreach (Type ancestor in implemented.GetInterfaces())
            inherited.Add(ancestor);
    return type.GetInterfaces().Where(implemented => !inherited.Contains(implemented));
}

static string MethodSignature(MethodInfo method) => string.Join(";",
    "access=" + MethodAccess(method),
    "return=" + TypeIdName(method.ReturnType),
    "returnNullability=" + new NullabilityInfoContext().Create(method.ReturnParameter).ReadState,
    "static=" + method.IsStatic,
    "abstract=" + method.IsAbstract,
    "virtual=" + (method.IsVirtual && !method.IsFinal),
    "final=" + method.IsFinal,
    "params=" + string.Join(",", method.GetParameters().Select(ParameterSignature)),
    "constraints=" + GenericConstraints(method.GetGenericArguments()));

static string ConstructorSignature(ConstructorInfo constructor) =>
    "access=" + MethodAccess(constructor) + ";params=" + string.Join(",", constructor.GetParameters().Select(ParameterSignature));

static string TypeSignature(Type type)
{
    string kind = type.IsEnum ? "enum" : type.IsInterface ? "interface" : type.IsValueType ? "struct" : "class";
    string baseType = type.BaseType is null ? "" : TypeIdName(type.BaseType);
    string interfaces = string.Join(",", DirectInterfaces(type).Select(TypeIdName).OrderBy(value => value));
    string enumValues = type.IsEnum
        ? string.Join(",", type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .OrderBy(field => field.MetadataToken)
            .Select(field => field.Name + "=" + Convert.ToString(field.GetRawConstantValue(), CultureInfo.InvariantCulture)))
        : "";
    return string.Join(";",
        "access=" + TypeAccess(type),
        "kind=" + kind,
        "abstract=" + type.IsAbstract,
        "sealed=" + type.IsSealed,
        "base=" + baseType,
        "interfaces=" + interfaces,
        "enumUnderlying=" + (type.IsEnum ? TypeIdName(Enum.GetUnderlyingType(type)) : ""),
        "enumValues=" + enumValues,
        "constraints=" + GenericConstraints(type.GetGenericArguments()));
}

static string FieldSignature(FieldInfo field)
{
    string value = "";
    if (field.IsLiteral)
    {
        object? raw = field.GetRawConstantValue();
        value = raw is null ? "null" : Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "";
    }
    var nullability = new NullabilityInfoContext().Create(field);
    return string.Join(";", "access=" + FieldAccess(field), "type=" + TypeIdName(field.FieldType), "readNullability=" + nullability.ReadState,
        "writeNullability=" + nullability.WriteState, "static=" + field.IsStatic,
        "const=" + field.IsLiteral, "readonly=" + field.IsInitOnly, "value=" + value);
}

static bool IsVisible(MemberInfo member) => member switch
{
    ConstructorInfo constructor => IsExternallyAccessible(constructor),
    MethodInfo method => IsExternallyAccessible(method) && (!method.IsSpecialName || method.Name.StartsWith("op_", StringComparison.Ordinal)),
    PropertyInfo property => (property.GetMethod is not null && IsExternallyAccessible(property.GetMethod)) ||
        (property.SetMethod is not null && IsExternallyAccessible(property.SetMethod)),
    FieldInfo field => (field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly) && !field.IsSpecialName,
    EventInfo eventInfo => eventInfo.AddMethod is not null && IsExternallyAccessible(eventInfo.AddMethod),
    _ => false,
};

static string AccessorAccess(MethodInfo? accessor) => accessor is not null && IsExternallyAccessible(accessor)
    ? MethodAccess(accessor)
    : "";

static bool IsInitOnly(PropertyInfo property) => property.SetMethod?.ReturnParameter
    .GetRequiredCustomModifiers().Any(type => type.FullName == "System.Runtime.CompilerServices.IsExternalInit") == true;

var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
var entries = new List<object>();
foreach (Type type in assembly.GetTypes().Where(IsExternallyVisibleType).OrderBy(type => DeclaringTypeId(type)))
{
    string declaringType = DeclaringTypeId(type);
    entries.Add(new { Id = "T:" + declaringType, Kind = "type", DeclaringType = declaringType, Signature = TypeSignature(type) });
    foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Where(IsVisible)
        .OrderBy(member => member.MemberType).ThenBy(member => member.Name).ThenBy(member => member.MetadataToken))
    {
        string id;
        string signature;
        switch (member)
        {
            case ConstructorInfo constructor:
                id = ConstructorId(constructor);
                signature = ConstructorSignature(constructor);
                break;
            case MethodInfo method:
                id = MethodId(method);
                signature = MethodSignature(method);
                break;
            case PropertyInfo property:
                var propertyNullability = new NullabilityInfoContext().Create(property);
                string propertyParameters = string.Join(",", property.GetIndexParameters().Select(parameter => TypeIdName(parameter.ParameterType)));
                id = $"P:{declaringType}.{property.Name}" + (propertyParameters.Length == 0 ? "" : "(" + propertyParameters + ")");
                signature = string.Join(";", "type=" + TypeIdName(property.PropertyType),
                    "readNullability=" + propertyNullability.ReadState,
                    "writeNullability=" + propertyNullability.WriteState,
                    "static=" + ((property.GetMethod ?? property.SetMethod)?.IsStatic == true),
                    "get=" + AccessorAccess(property.GetMethod), "set=" + AccessorAccess(property.SetMethod),
                    "init=" + IsInitOnly(property),
                    "getAbstract=" + (property.GetMethod?.IsAbstract == true),
                    "setAbstract=" + (property.SetMethod?.IsAbstract == true),
                    "getVirtual=" + (property.GetMethod?.IsVirtual == true && property.GetMethod?.IsFinal == false),
                    "setVirtual=" + (property.SetMethod?.IsVirtual == true && property.SetMethod?.IsFinal == false),
                    "index=" + string.Join(",", property.GetIndexParameters().Select(ParameterSignature)));
                break;
            case FieldInfo field:
                id = $"F:{declaringType}.{field.Name}";
                signature = FieldSignature(field);
                break;
            case EventInfo eventInfo:
                id = $"E:{declaringType}.{eventInfo.Name}";
                var addMethod = eventInfo.AddMethod;
                var eventNullability = new NullabilityInfoContext().Create(eventInfo);
                signature = string.Join(";", "type=" + TypeIdName(eventInfo.EventHandlerType ?? typeof(object)),
                    "access=" + (addMethod is null ? "" : MethodAccess(addMethod)),
                    "readNullability=" + eventNullability.ReadState, "writeNullability=" + eventNullability.WriteState,
                    "static=" + (addMethod?.IsStatic == true), "abstract=" + (addMethod?.IsAbstract == true),
                    "virtual=" + (addMethod?.IsVirtual == true && addMethod?.IsFinal == false));
                break;
            default:
                continue;
        }
        entries.Add(new { Id = id, Kind = "member", DeclaringType = declaringType, Signature = signature });
    }

    if (declaringType == "PlcComm.KvHostLink.KvHostLinkPlcProfiles")
    {
        var getNames = type.GetMethod("GetNames", BindingFlags.Public | BindingFlags.Static);
        if (getNames?.Invoke(null, null) is IEnumerable names)
        {
            foreach (string name in names.Cast<object>().Select(value => value.ToString() ?? "").OrderBy(value => value))
                entries.Add(new { Id = "V:" + declaringType + ".ProfileName(" + name + ")", Kind = "contract-value", DeclaringType = declaringType, Signature = name });
        }
        var getDescriptors = type.GetMethod("GetProfileDescriptors", BindingFlags.Public | BindingFlags.Static);
        if (getDescriptors?.Invoke(null, null) is IEnumerable descriptors)
        {
            foreach (object descriptor in descriptors)
            {
                Type descriptorType = descriptor.GetType();
                string canonicalName = descriptorType.GetProperty("CanonicalName")?.GetValue(descriptor)?.ToString() ?? "";
                string displayName = descriptorType.GetProperty("DisplayName")?.GetValue(descriptor)?.ToString() ?? "";
                string connectable = descriptorType.GetProperty("Connectable")?.GetValue(descriptor)?.ToString() ?? "";
                string baseProfile = descriptorType.GetProperty("BaseProfile")?.GetValue(descriptor)?.ToString() ?? "";
                entries.Add(new {
                    Id = "V:" + declaringType + ".ProfileDescriptor(" + canonicalName + ")",
                    Kind = "contract-value",
                    DeclaringType = declaringType,
                    Signature = string.Join("|", canonicalName, displayName, connectable, baseProfile),
                });
            }
        }
    }
}

Console.WriteLine(JsonSerializer.Serialize(entries.OrderBy(entry => entry.GetType().GetProperty("Id")!.GetValue(entry)),
    new JsonSerializerOptions { WriteIndented = true }));
'''


class GateError(RuntimeError):
    """Expected policy or input failure."""


@dataclass(frozen=True)
class Difference:
    framework: str
    change: str
    symbol: str
    before: str | None
    after: str | None


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline-metadata", required=True, type=Path)
    parser.add_argument(
        "--candidate-assembly-root",
        required=True,
        type=Path,
        help="Configuration output root containing net8.0, net9.0, and net10.0 assembly directories",
    )
    parser.add_argument("--classifications", required=True, type=Path)
    parser.add_argument("--repository-root", type=Path, default=Path.cwd())
    parser.add_argument("--require-release-version-policy", action="store_true")
    return parser.parse_args()


def load_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise GateError(f"{path} must contain one JSON object")
    return value


def package_path(metadata: dict[str, Any], work_dir: Path) -> Path:
    override = os.environ.get("PLC_COMM_API_BASELINE_PACKAGE")
    if override:
        path = Path(override).resolve()
        if not path.is_file():
            raise GateError(f"PLC_COMM_API_BASELINE_PACKAGE does not exist: {path}")
        return path
    target = work_dir / str(metadata["fileName"])
    request = urllib.request.Request(str(metadata["url"]), headers={"User-Agent": "plc-comm-api-diff/1"})
    with urllib.request.urlopen(request, timeout=60) as response, target.open("wb") as output:
        output.write(response.read())
    return target


def verify_digest(path: Path, expected: str) -> None:
    actual = hashlib.sha256(path.read_bytes()).hexdigest()
    if actual.lower() != expected.lower():
        raise GateError(f"baseline package SHA-256 mismatch: expected {expected}, got {actual}")


def git_blob_digest(content: bytes) -> str:
    header = f"blob {len(content)}\0".encode("ascii")
    return hashlib.sha1(header + content).hexdigest()


def load_prior_contract(metadata: dict[str, Any]) -> tuple[str, dict[str, list[str]]]:
    provenance = metadata.get("contractProvenance")
    if not isinstance(provenance, dict):
        raise GateError("baseline metadata requires immutable contractProvenance")
    commit = str(provenance.get("commit", ""))
    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise GateError("contractProvenance.commit must be one full Git commit SHA")
    files = provenance.get("files")
    expected_scopes = {"README", "standard-user-pages", "generated-api-reference", "maintained-samples"}
    if not isinstance(files, list) or {item.get("scope") for item in files if isinstance(item, dict)} != expected_scopes:
        raise GateError("contractProvenance must cover all four prior stable contract scopes")

    override = os.environ.get("PLC_COMM_API_BASELINE_CONTRACT_ROOT")
    root = Path(override).resolve() if override else None
    if root is not None and not root.is_dir():
        raise GateError(f"PLC_COMM_API_BASELINE_CONTRACT_ROOT does not exist: {root}")
    raw_base = str(provenance.get("rawBaseUrl", "")).rstrip("/")
    if root is None and commit not in raw_base:
        raise GateError("contractProvenance.rawBaseUrl must be pinned to contractProvenance.commit")

    result: dict[str, list[str]] = {scope: [] for scope in expected_scopes}
    seen_paths: set[str] = set()
    for item in files:
        if not isinstance(item, dict):
            raise GateError("each contractProvenance file must be an object")
        path_text = str(item.get("path", "")).replace("\\", "/")
        scope = str(item.get("scope", ""))
        expected_blob = str(item.get("gitBlob", ""))
        if not path_text or path_text.startswith("/") or ".." in path_text.split("/"):
            raise GateError(f"invalid contractProvenance path: {path_text!r}")
        if path_text in seen_paths:
            raise GateError(f"duplicate contractProvenance path: {path_text}")
        if scope not in expected_scopes or not re.fullmatch(r"[0-9a-f]{40}", expected_blob):
            raise GateError(f"invalid contractProvenance entry for {path_text}")
        seen_paths.add(path_text)
        if root is not None:
            content = (root / Path(path_text)).read_bytes()
        else:
            request = urllib.request.Request(f"{raw_base}/{path_text}", headers={"User-Agent": "plc-comm-api-diff/1"})
            with urllib.request.urlopen(request, timeout=60) as response:
                content = response.read()
        actual_blob = git_blob_digest(content)
        if actual_blob != expected_blob:
            raise GateError(
                f"prior contract Git blob mismatch for {path_text}: expected {expected_blob}, got {actual_blob}"
            )
        try:
            result[scope].append(content.decode("utf-8-sig"))
        except UnicodeDecodeError as error:
            raise GateError(f"prior contract file is not UTF-8 text: {path_text}") from error
    if any(not entries for entries in result.values()):
        raise GateError("every prior stable contract scope must contain at least one immutable file")
    return commit, result


def build_inspector(work_dir: Path) -> Path:
    project_dir = work_dir / "inspector"
    project_dir.mkdir()
    (project_dir / "ApiSurfaceInspector.csproj").write_text(
        '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
        '<TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings>'
        '<Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors>'
        '</PropertyGroup></Project>',
        encoding="utf-8",
    )
    (project_dir / "Program.cs").write_text(CSHARP_INSPECTOR, encoding="utf-8")
    build_environment = os.environ.copy()
    build_environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0"
    subprocess.run(
        [
            "dotnet", "build", str(project_dir / "ApiSurfaceInspector.csproj"),
            "-c", "Release", "--nologo", "-p:UseSharedCompilation=false",
        ],
        check=True,
        env=build_environment,
    )
    return project_dir / "bin" / "Release" / "net10.0" / "ApiSurfaceInspector.dll"


def inspect(inspector: Path, assembly: Path) -> list[dict[str, str]]:
    result = subprocess.run(
        ["dotnet", str(inspector), str(assembly.resolve())],
        check=True,
        capture_output=True,
        text=True,
    )
    value = json.loads(result.stdout)
    if not isinstance(value, list):
        raise GateError("API inspector returned an invalid document")
    return value


def surface_map(entries: list[dict[str, str]], framework: str, side: str) -> dict[str, dict[str, str]]:
    result: dict[str, dict[str, str]] = {}
    for entry in entries:
        if not isinstance(entry, dict) or not all(isinstance(entry.get(key), str) for key in ("Id", "DeclaringType", "Signature")):
            raise GateError(f"{framework} {side} API inspector entry is invalid")
        symbol = entry["Id"]
        if symbol in result:
            raise GateError(f"duplicate API ID in {framework} {side} surface: {symbol}")
        result[symbol] = entry
    return result


def diff_surfaces(
    framework: str,
    before: list[dict[str, str]],
    after: list[dict[str, str]],
) -> list[Difference]:
    old = surface_map(before, framework, "baseline")
    new = surface_map(after, framework, "candidate")
    removed_types = {symbol for symbol in old.keys() - new.keys() if symbol.startswith("T:")}
    added_types = {symbol for symbol in new.keys() - old.keys() if symbol.startswith("T:")}

    def belongs_to(entry: dict[str, str], types: set[str]) -> bool:
        return "T:" + entry["DeclaringType"] in types

    differences: list[Difference] = []
    for symbol in sorted(old.keys() - new.keys()):
        if not symbol.startswith("T:") and belongs_to(old[symbol], removed_types):
            continue
        differences.append(Difference(framework, "removed", symbol, old[symbol]["Signature"], None))
    for symbol in sorted(new.keys() - old.keys()):
        if not symbol.startswith("T:") and belongs_to(new[symbol], added_types):
            continue
        differences.append(Difference(framework, "added", symbol, None, new[symbol]["Signature"]))
    for symbol in sorted(old.keys() & new.keys()):
        if old[symbol]["Signature"] != new[symbol]["Signature"]:
            differences.append(Difference(framework, "changed", symbol, old[symbol]["Signature"], new[symbol]["Signature"]))
    return differences


def require_path(root: Path, relative: str, needle: str | None = None) -> None:
    path = (root / relative).resolve()
    if not path.is_file():
        raise GateError(f"required evidence file is missing: {relative}")
    if needle and needle not in path.read_text(encoding="utf-8"):
        raise GateError(f"required evidence text {needle!r} is missing from {relative}")


def validate_evidence(
    category: str,
    evidence: dict[str, Any],
    root: Path,
    prior_commit: str,
    prior_contract: dict[str, list[str]],
) -> None:
    if not evidence.get("decision") or not evidence.get("rationale"):
        raise GateError(f"{category} evidence requires decision and rationale")
    if category == "documented-contract":
        require_path(root, str(evidence.get("migrationPath", "")), str(evidence["decision"]))
        require_path(root, str(evidence.get("changelogPath", "")), str(evidence.get("changelogNeedle", "**Breaking:**")))
        docs = evidence.get("documentationEvidence")
        if not isinstance(docs, list) or not docs:
            raise GateError("documented-contract evidence requires documentationEvidence")
        for item in docs:
            if not isinstance(item, dict) or not item.get("path") or not item.get("needle"):
                raise GateError("each documented-contract documentation entry requires path and needle")
            require_path(root, str(item["path"]), str(item["needle"]))
        if evidence.get("versionPolicy") != "major-version-required-before-release":
            raise GateError("documented incompatible changes require a major-version release disposition")
        term = str(evidence.get("priorContractTerm", ""))
        if evidence.get("priorContractCommit") != prior_commit or not term:
            raise GateError("documented-contract evidence must identify the immutable prior commit and search term")
        if not any(term in text for texts in prior_contract.values() for text in texts):
            raise GateError(f"documented-contract term {term!r} is absent from the immutable prior contract")
    elif category == "undocumented-public":
        term = str(evidence.get("priorContractTerm", ""))
        if evidence.get("priorContractCommit") != prior_commit or not term:
            raise GateError("undocumented-public evidence must identify the immutable prior commit and search term")
        for scope, texts in prior_contract.items():
            if any(term in text for text in texts):
                raise GateError(f"undocumented-public term {term!r} was found in prior {scope}")
    elif category == "additive":
        if evidence.get("documentationDisposition") not in {"documented", "intentionally-undocumented"}:
            raise GateError("additive evidence requires a documentationDisposition")
        for path in evidence.get("documentationPaths", []):
            require_path(root, str(path))
    elif category == "generated-or-noncontract":
        if not evidence.get("generator"):
            raise GateError("generated-or-noncontract evidence requires a narrow generator identity")


def enforce_classifications(
    differences: list[Difference],
    config: dict[str, Any],
    root: Path,
    *,
    baseline_version: str,
    candidate_version: str,
    require_release_version_policy: bool,
    expected_frameworks: set[str],
    prior_contract_commit: str,
    prior_contract: dict[str, list[str]],
) -> None:
    evidence_sets = config.get("evidenceSets")
    records = config.get("classifications")
    if config.get("schemaVersion") != 2 or not isinstance(evidence_sets, dict) or not isinstance(records, list):
        raise GateError("classification file must use schemaVersion 2 with evidenceSets and classifications")
    actual: dict[tuple[str, str, str], Difference] = {}
    for difference in differences:
        key = (difference.framework, difference.change, difference.symbol)
        if key in actual:
            raise GateError(f"duplicate actual API difference: {' '.join(key)}")
        actual[key] = difference
    classified: dict[tuple[str, str, str], dict[str, Any]] = {}
    has_documented_break = False
    for record in records:
        change = str(record.get("change", ""))
        symbol = str(record.get("symbol", ""))
        category = str(record.get("category", ""))
        frameworks = record.get("frameworks")
        if change not in {"added", "removed", "changed"}:
            raise GateError(f"invalid classified change kind: {change!r}")
        if category not in ALLOWED_CATEGORIES:
            raise GateError(f"invalid classification category: {category!r}")
        if not symbol or any(token in symbol for token in ("*", "?", "[", "]")):
            raise GateError(f"classification symbols must be exact and non-wildcard: {symbol!r}")
        if not isinstance(frameworks, list) or set(frameworks) != expected_frameworks or len(frameworks) != len(expected_frameworks):
            raise GateError(f"classification {change} {symbol} must explicitly cover each target framework")
        expected_before = record.get("before")
        expected_after = record.get("after")
        if change == "added" and (expected_before is not None or not isinstance(expected_after, str)):
            raise GateError(f"added classification must pin null before and exact after signature: {symbol}")
        if change == "removed" and (not isinstance(expected_before, str) or expected_after is not None):
            raise GateError(f"removed classification must pin exact before and null after signature: {symbol}")
        if change == "changed" and (not isinstance(expected_before, str) or not isinstance(expected_after, str)):
            raise GateError(f"changed classification must pin exact before and after signatures: {symbol}")
        evidence_name = record.get("evidence")
        evidence = evidence_sets.get(evidence_name)
        if not isinstance(evidence, dict):
            raise GateError(f"classification {change} {symbol} has unknown evidence set {evidence_name!r}")
        validate_evidence(category, evidence, root, prior_contract_commit, prior_contract)
        if category == "additive" and change != "added":
            raise GateError(f"additive classification cannot describe {change}: {symbol}")
        if category == "documented-contract" and change in {"removed", "changed"}:
            has_documented_break = True
        for framework in frameworks:
            key = (str(framework), change, symbol)
            if key in classified:
                raise GateError(f"duplicate classification: {' '.join(key)}")
            difference = actual.get(key)
            if difference is not None and (difference.before != expected_before or difference.after != expected_after):
                raise GateError(
                    f"classified signature drift in {' '.join(key)}:\n"
                    f"  expected before={expected_before!r}\n  actual before={difference.before!r}\n"
                    f"  expected after={expected_after!r}\n  actual after={difference.after!r}"
                )
            classified[key] = record

    missing = sorted(set(actual) - set(classified))
    stale = sorted(set(classified) - set(actual))
    if missing:
        raise GateError("unclassified API differences:\n" + "\n".join(
            f"  {framework} {change}: {symbol}" for framework, change, symbol in missing))
    if stale:
        raise GateError("stale API classifications no longer matching a difference:\n" + "\n".join(
            f"  {framework} {change}: {symbol}" for framework, change, symbol in stale))
    if require_release_version_policy and has_documented_break:
        baseline_major = int(baseline_version.split(".", 1)[0])
        candidate_major = int(candidate_version.split(".", 1)[0])
        if candidate_major <= baseline_major:
            raise GateError(
                f"documented incompatible changes require a major version above {baseline_version}; candidate is {candidate_version}"
            )


def project_version(root: Path) -> str:
    text = (root / "Directory.Build.props").read_text(encoding="utf-8")
    match = re.search(r"<Version>([^<]+)</Version>", text)
    if not match:
        raise GateError("Directory.Build.props does not declare Version")
    return match.group(1)


def main() -> int:
    args = parse_args()
    root = args.repository_root.resolve()
    metadata = load_json(args.baseline_metadata)
    classifications = load_json(args.classifications)
    if metadata.get("schemaVersion") != 2:
        raise GateError("baseline metadata must use schemaVersion 2")
    if classifications.get("baselineIdentity") != metadata.get("identity"):
        raise GateError("classification baselineIdentity does not match baseline metadata")
    framework_entries = metadata.get("frameworks")
    expected_frameworks = {"net8.0", "net9.0", "net10.0"}
    if not isinstance(framework_entries, dict) or set(framework_entries) != expected_frameworks:
        raise GateError("baseline metadata must define independent net8.0, net9.0, and net10.0 assemblies")
    candidate_assemblies = {
        framework: args.candidate_assembly_root / framework / f"{metadata['packageId']}.dll"
        for framework in expected_frameworks
    }
    for framework, candidate in candidate_assemblies.items():
        if not candidate.is_file():
            raise GateError(f"{framework} candidate assembly does not exist: {candidate}")
    prior_contract_commit, prior_contract = load_prior_contract(metadata)

    local_work = root / "local_folder"
    local_work.mkdir(exist_ok=True)
    differences: list[Difference] = []
    with tempfile.TemporaryDirectory(prefix="documented-api-diff-", dir=local_work) as temp_name:
        work_dir = Path(temp_name)
        package = package_path(metadata, work_dir)
        verify_digest(package, str(metadata["sha256"]))
        inspector = build_inspector(work_dir)
        with zipfile.ZipFile(package) as archive:
            for framework in sorted(expected_frameworks):
                entry = framework_entries[framework]
                if not isinstance(entry, dict) or not entry.get("assemblyEntry"):
                    raise GateError(f"baseline metadata has no assemblyEntry for {framework}")
                baseline_assembly = work_dir / f"baseline-{framework}.dll"
                baseline_assembly.write_bytes(archive.read(str(entry["assemblyEntry"])))
                before = inspect(inspector, baseline_assembly)
                after = inspect(inspector, candidate_assemblies[framework])
                differences.extend(diff_surfaces(framework, before, after))

    enforce_classifications(
        differences,
        classifications,
        root,
        baseline_version=str(metadata["version"]),
        candidate_version=project_version(root),
        require_release_version_policy=args.require_release_version_policy,
        expected_frameworks=expected_frameworks,
        prior_contract_commit=prior_contract_commit,
        prior_contract=prior_contract,
    )
    counts = {category: 0 for category in sorted(ALLOWED_CATEGORIES)}
    for record in classifications["classifications"]:
        counts[record["category"]] += 1
    print(
        f"[OK] Classified {len(differences)} framework-specific API differences "
        f"against {metadata['identity']} and prior contract {prior_contract_commit}."
    )
    print("Classification counts: " + ", ".join(f"{key}={value}" for key, value in counts.items()))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (GateError, KeyError, OSError, subprocess.CalledProcessError, zipfile.BadZipFile) as error:
        print(f"[ERROR] {error}", file=sys.stderr)
        raise SystemExit(1)

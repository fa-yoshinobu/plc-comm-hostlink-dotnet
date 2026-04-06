# Latest Communication Verification

This page keeps the current public summary only. Older detailed notes are not kept in the public documentation set.

## Current Retained Summary

- verified PLC model: `KV-7500`
- verified public surface: connection factory, typed reads/writes, mixed snapshots, bit-in-word updates, polling, single-request reads, chunked reads
- recommended first public test: `DM0` and `DM10`

## Practical Public Conclusions

- `DM` remains the safest first-run path
- typed views `:S`, `:D`, `:L`, and `:F` are part of the current public helper surface
- bit-in-word updates remain public through `WriteBitInWordAsync`

## Current Cautions

- keep large chunked reads out of the first smoke test
- use only the families in the current public register table

## Where Older Evidence Went

Public historical validation clutter was removed. Maintainer-only retained evidence now belongs under `internal_docs/`.

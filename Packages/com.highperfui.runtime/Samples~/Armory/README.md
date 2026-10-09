# SLG Armory Business Sample

Import this optional sample through Package Manager. Open `Scenes/Inventory.unity` and press Play, or use `Tools > HighPerfUI > Open Armory Sample`.

The normal screen is a local equipment inventory: filter/search, compare/equip, resource-consuming upgrades, locks, set defense and mixed seasonal reward grants. Diagnostics are opt-in. This is not a complete SLG game and does not connect to a server.

Runtime code is exported from ReferenceProject without behavioral rewrites. The sample assembly is `HighPerfUI.Reference`; do not import multiple versions simultaneously or copy it into an existing project with that same assembly/name space.

Editor sessions use deterministic data and do not overwrite local saves. Normal built players persist locally; `--fresh` disables persistence. Local checksum/backup/idempotency are correctness aids, not anti-cheat or server authority.

The portrait is an original generated raster asset. See `ArtProvenance.md`. Fonts use the available system font with a fallback; validate the target platform's Chinese glyphs and font licensing before shipping.

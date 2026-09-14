# Texas PRTS Asset Pipeline

PRTS exposes Texas's default front-facing battle Spine source under internal id `char_102_texas`.

Source page:

`https://prts.wiki/w/德克萨斯/spine`

PRTS data on that page points to:

`https://static.prts.wiki/spine/char/char_102_texas/char_102_texas/char_102_texas`

## Local download

In Unity run:

`ArknightsACT > Assets > Download Texas PRTS Spine Source`

The editor utility downloads the atlas, atlas texture pages and skeleton source into:

`Assets/_Game/Art/Characters/Texas/PRTS/Spine/`

The binary game assets are intentionally not committed to Git. This keeps the repository lightweight and keeps third-party art separate from gameplay code.

## Runtime rendering

The current prototype does **not** add a Spine runtime dependency. Until a compatible runtime or an offline frame-baking pipeline is selected, `TexasPlaceholderRig2D` remains the fallback presentation.

When Spine rendering is added, it must replace Presentation only. Physics, `CombatEntity`, attacks, skills, statuses and builds must not reference PRTS paths or Spine types.

## Distribution

PRTS states that game images, animations, audio and original game text belong to Hypergryph and affiliates. Treat downloaded files as prototype/reference assets unless you have the rights required for the intended distribution.

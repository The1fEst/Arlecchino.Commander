# Arlecchino Commander — brand assets

The same commedia dell'arte register as the framework it is written on: a bone harlequin mask, bell
diamonds on the horns, and a rhombus lattice standing in for the character grid of a terminal. What
tells the two apart is the wordmark — `ARLECCHINO` set small above `COMMANDER` — and the pair of
panel frames in the corner, one of them with the crimson title bar of the panel that has the focus.

## Palette

| Role | Hex |
| --- | --- |
| Ink (plate, eyes) | `#141317` |
| Bone (mask, wordmark) | `#EDE6D9` |
| Crimson (accent, bells, active panel) | `#C9382B` |
| Flame (the accent as words) | `#D75147` |
| Hairline (borders, panel frames) | `#2F2C28` |

Anything quieter than a name is one of seven warm grays, from `#C5C3BF` down to `#6C6760`. They are
spaced by contrast against the near-black rather than by lightness, and the quietest of them still
reads at three to one.

Type: any monospace stack, capitals, generously spaced.

## Files

| File | Use |
| --- | --- |
| `commander-banner.svg` | 1280×520 hero for the top of the README |
| `screenshots/*.png` | the gallery, photographed from a terminal by `tools/shots.cs shoot` |
| `demo.png` | the animation the README opens with, recorded by `tools/shots.cs tape` |

## README snippet

```html
<p align="center">
  <img src="assets/commander-banner.svg" alt="Arlecchino Commander" width="820">
</p>
```

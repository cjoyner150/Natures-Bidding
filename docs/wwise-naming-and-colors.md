# Wwise naming and colors

Use the same category color for containers, events, and their output bus. Set the color on the category work unit and let its children inherit. Set bus colors explicitly because they share Main Audio Bus as their parent.

| Category | Color | Wwise palette index | Object prefix |
| --- | --- | --- | --- |
| Music | Indigo | 1 | `MX_` |
| Ambience | Olive | 19 | `AMB_` |
| UI | Blue | 2 | `UI_` |
| SFX | Orange | 8 | `SFX_` |
| Dialogue | Pink | 24 | `DLG_` |
| States, switches, and game parameters | Teal | 3 | Keep descriptive group names |
| Excluded music alternates | Gray | 0 | End names with `_Alt` |

Dialogue currently routes through the SFX bus. Its authoring objects and events are pink, while the shared SFX bus remains orange. The Music conversion ShareSet uses indigo. Built-in defaults and factory presets retain their existing names and colors.

## Names

Use underscores between category and purpose. Keep existing game terms such as `Cliff`, `Lava`, `RockSlide`, and `ManMask` consistent with event names and state values.

| Object | Pattern | Examples |
| --- | --- | --- |
| Playback event | `Play_<Category>_<Purpose>` | `Play_MX_System`, `Play_DLG_Auctioneer`, `Play_UI_Bid_Adjust` |
| Stop event | `Stop_<Category>_<Purpose>` | `Stop_MX_System`, `Stop_AMB_Lava` |
| Audio container | `<Category>_<Context>_<Purpose>` | `AMB_Forest_Bed`, `SFX_Jump`, `UI_Bid_Up` |
| Music segment | `MX_<Context>_<Role>` | `MX_Lobby_Loop`, `MX_Cliff_Intro`, `MX_Bidding_Loop` |
| Music track | `MX_<Context>_<Role>` | `MX_Lobby_Player_Mix`, `MX_Menu_Track`, `MX_Cliff_Intro_Track` |
| Sound variation | Lowercase category and purpose, then a two-digit number | `sfx_jump_01`, `ui_bid_down_02`, `dlg_auctioneer_03` |
| Organizational folder | A short category name | `Bidding`, `Generic`, `Shop` |

A single sound can omit a number when it has no numbered variations, such as `amb_forest_silence`. Keep imported AudioFileSource names and original WAV filenames as provenance for the audio export. They do not need to match the shorter authoring object names.

Use each category's existing work units under Containers and Events. `Play_SFX_Death` now belongs to the SFX Events work unit. Excluded Lobby alternates retain their exclusion settings and inherit gray from the alternate segment where applicable.

## Unity and SoundBanks

`Play_Auctioneer` was renamed to `Play_DLG_Auctioneer`. Its Wwise GUID and Unity reference asset GUID stayed the same. Its event ID changed from `4115709719` to `3605236953`, and the Unity event reference was updated. Use the new event name for any future string-based calls.

After changing authoring data, run `bash scripts/generate-wwise-banks.sh` from the repository root. This generates Mac, Windows, and Linux and updates both integrity manifests. Check freshness with `bash scripts/generate-wwise-banks.sh --verify`.

## Cleanup verification

The September 7, 2026 cleanup renamed 74 objects and verified effective colors on 207 objects. All 1,074 Wwise object GUIDs stayed the same. A comparison against the saved pre-cleanup project found no changes to playback settings, routing, timing, audio source paths, or inclusion settings.

The reference audit passed for 37 work units, 370 object and audio references, 37 Unity Wwise reference assets, and the serialized audio references found in 10 custom components. All 20 event banks and their media were present on Mac, Windows, and Linux after generation. This was an authoring and bank validation pass, without a new gameplay listening test.

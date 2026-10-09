# Wwise naming and colors

Updated October 8, 2026. Use the same descriptive sound family in REAPER delivery names, exported WAVs, Wwise objects, and Events. Naming categories describe the audio's purpose. They do not change its Wwise object type or output bus.

| Category | Color | Wwise palette index | Object prefix |
| --- | --- | --- | --- |
| Music | Indigo | 1 | `MX_` |
| Ambience | Olive | 19 | `AMB_` |
| UI | Blue | 2 | `UI_` |
| Gameplay effects | Orange | 8 | `GP_` |
| Voice, including dialogue and character reactions | Existing voice color | 24 for Auctioneer | `VO_` |
| States, switches, and game parameters | Teal | 3 | Keep descriptive group names |
| Excluded music alternates | Gray | 0 | End names with `_Alt` |

The broad SFX bus retains its name and routing. Auctioneer voice routes through that bus and remains pink. Jump grunts retain their existing gameplay hierarchy, orange color, and mix settings. The naming migration does not change colors, audio settings, hierarchy parents, or mix routing. The Music conversion ShareSet uses indigo. Built-in defaults and factory presets retain their existing names and colors.

## Names

Use underscores between category and purpose. Keep existing game terms such as `Cliff`, `Lava`, `RockSlide`, and `ManMask` consistent with event names and state values.

| Object | Pattern | Examples |
| --- | --- | --- |
| Playback event | `Play_<Category>_<Purpose>`, with optional context before purpose | `Play_GP_Player_DoubleJump`, `Play_VO_Auctioneer`, `Play_UI_Bid_Adjust` |
| Stop event | `Stop_<Category>_<Purpose>` | `Stop_MX_System`, `Stop_AMB_Lava` |
| Audio container | `<Category>_<Purpose>`, with optional context before purpose | `AMB_Forest_Bed`, `GP_Player_DoubleJump`, `VO_Player_JumpGrunt`, `UI_Bid_Up` |
| Music segment | `MX_<Context>_<Role>` | `MX_Lobby_Loop`, `MX_Cliff_Intro`, `MX_Bidding_Loop` |
| Music track | `MX_<Context>_<Role>` | `MX_Lobby_Player_Mix`, `MX_Menu_Track`, `MX_Cliff_Intro_Track` |
| Sound variation and delivery WAV | Lowercase category, context, purpose, then a two-digit number | `gp_player_double_jump_01`, `vo_player1_jump_grunt_02`, `vo_auctioneer_03` |
| Organizational folder | A short category name | `Bidding`, `Generic`, `Shop` |

A single sound can omit a number when it has no numbered variations, such as `amb_forest_silence`. Keep working revisions distinct with `v01`, `v02`, or their existing revision folders. New Events use stable names without variation or revision suffixes. The existing `Stop_AMB_Forest_01` Event retains its name and identity. Shared source WAVs may serve multiple Sounds. The bid-adjustment Up and Down sounds currently use the same `ui_bid_click_01` through `06` sources with their existing playback settings.

Give inactive source objects distinct names, such as `gp_player_parry_success_01_alternate`. Keep the active source's name aligned with its WAV stem so Wwise can load the work unit without duplicate source names.

Gameplay uses `Gameplay Work Unit`, and Auctioneer voice uses `Voice Work Unit`, under Containers and Events. Jump grunts retain their existing gameplay parents and use `VO_` names. Excluded Lobby alternates retain their inclusion settings and gray color. The built-in `Originals/SFX` folder remains because it is Wwise's technical source category. Its user-authored gameplay and voice filenames use `gp_` and `vo_`.

## Gameplay and voice families

| Gameplay meaning | Wwise family | REAPER delivery stem |
| --- | --- | --- |
| Attack slash | `GP_Player_AttackSlash` | `gp_player_attack_slash` |
| Taking a hit | `GP_Player_TakeHit` | `gp_player_take_hit` |
| Successful parry | `GP_Player_ParrySuccess` | `gp_player_parry_success` |
| Double jump | `GP_Player_DoubleJump` | `gp_player_double_jump` |
| Death | `GP_Player_Death` | `gp_player_death` |
| Warp | `GP_Player_Warp` | `gp_player_warp` |
| Cliff fall | `GP_Player_CliffFall` | `gp_player_cliff_fall` |
| Shield | `GP_Player_Shield` | `gp_player_shield` |
| Shield fail | `GP_Player_Shield_Fail` | `gp_player_shield_fail` |
| Rockslide | `GP_RockSlide` | `gp_rock_slide` |
| ManMask explosion | `GP_ManMask_Explosion` | `gp_man_mask_explosion` |
| Jump grunt | `VO_Player_JumpGrunt` | `vo_player1_jump_grunt` through `vo_player4_jump_grunt` |
| Auctioneer | `VO_Auctioneer` | `vo_auctioneer` |

REAPER also has `gp_player_dash` as a working family. This naming migration does not create a Wwise Dash event or import additional audio.

## Unity and SoundBanks

The October 8 migration replaces gameplay `Play_SFX_`/`Stop_SFX_` Events with their `GP_` families and names Auctioneer and jump-grunt Events `Play_VO_Auctioneer` and `Play_VO_Player_JumpGrunt`. Existing Wwise object GUIDs, Unity asset GUIDs, and serialized assignments remain intact. Name-derived Event IDs and auto-defined bank filenames change, so Unity Event reference names and IDs must match the regenerated banks. Use the current event names for future string-based calls.

After changing authoring data, run `bash scripts/generate-wwise-banks.sh` from the repository root. This generates Mac, Windows, and Linux and updates both integrity manifests. Check freshness with `bash scripts/generate-wwise-banks.sh --verify`.

On the current Mac, Unity's saved installation setting still names an absent 2025.1.9 app. The October 8 generation used the installed Authoring version without changing that setting:

```sh
WWISE_CONSOLE='/Applications/Audiokinetic/Wwise_2025.1.11.9262/Wwise.app/Contents/Tools/WwiseConsole.sh' bash scripts/generate-wwise-banks.sh
```

## Cleanup verification

The September 7, 2026 cleanup renamed 74 objects and verified effective colors on 207 objects. All 1,074 Wwise object GUIDs stayed the same. A comparison against the saved pre-cleanup project found no changes to playback settings, routing, timing, audio source paths, or inclusion settings. At that stage, Auctioneer was renamed from `Play_Auctioneer` to `Play_DLG_Auctioneer`; the October 8 convention supersedes that name with `Play_VO_Auctioneer`.

The reference audit passed for 37 work units, 370 object and audio references, 37 Unity Wwise reference assets, and the serialized audio references found in 10 custom components. All 20 event banks and their media were present on Mac, Windows, and Linux after generation. This was an authoring and bank validation pass, without a new gameplay listening test.

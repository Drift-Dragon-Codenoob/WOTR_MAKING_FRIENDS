# MAKING FRIENDS (Summoner Mod)

**Synthesist fork — development candidate, 1 November 2026.** Work on this fork is on the `synthesist` branch. It includes the original-PF1e Synthesist implementation, 59 cosmetic appearance candidates, four new selectable feats and the reviewed lifecycle/build repairs. The complete archetype is **not yet accepted for release**; version 0.3.0 remains reserved for a tested release. Player packages retain the existing mod ID and baseline version metadata.

Start with the [current player handoff](docs/TESTING_HANDOFF.md). See the [code review and remaining release blockers](docs/SYNTHESIST_CODE_REVIEW.md), [implementation evidence](docs/SYNTHESIST_IMPLEMENTATION.md), [appearance catalogue](docs/SYNTHESIST_APPEARANCES.md) and [feat guide](docs/SYNTHESIST_FEATS.md). David performs in-game testing; current player packages contain no automated acceptance/profiling interface.

The original Making Friends documentation and download links below describe Balkoth's upstream mod. Those download links do not supply this fork's Synthesist test candidate. This fork requires ModMenu 2.0.14 or newer; use the verified player ZIP for its experimental build.

## [![Download zip](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/blob/master/DownloadButton.png?raw=true "Download zip")](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/releases/latest/download/WOTR_MAKING_FRIENDS.zip)

Requires ModMenu:
https://github.com/WittleWolfie/ModMenu
![alt text](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/blob/master/modmenumakingfriends.png)



## How to Install

```
1. Download and install Unity Mod Manager, make sure it is at least version 0.23.0
2. Run Unity Mod Manger and set it up to find Wrath of the Righteous
3. Download the WOTR_MAKING_FRIENDS mod
4. Install the mod by dragging the zip file from step 3 into the Unity Mod Manager window under the Mods tab
```
## Added Content
```
* Added Summoner Class
* 57 Different bodies for your Eidolon
* 20 New Unique Spells, including the ever elusive Black Tentacles and Infernal Healing
* 22 Eidolon Subtypes
* 61 Evolutions to choose from some with multiple ranks
* 828 Blueprints
```
## Change Log
```
* 03/10/2024 (0.1.9) Added localization for the Pincers evolution.
* 03/10/2024 (0.1.9) Fixed Transmogrify to no longer give additional attacks.
* 03/10/2024 (0.1.9) Fixed equipment slots being locked even if you have the arms evolution.
* 10/21/2023 (0.1.8) @ifarmpandas Addition of share spells 
* 06/06/2024 (0.2.1) Bite attack now toggleable as a primary or secondary attack
* 06/06/2024 (0.2.1) 1 evolution point natural attacks will now properly override empty slots. 
* 01/18/2025 (0.2.4) Fixed spell lists being added to other classes.
* 01/18/2025 (0.2.4) TTT Compatability.
* 01/18/2025 (0.2.4) Fixed bite attack to properly work with tripping.
```

## Known Issues
```
* Eidolons do not qualify for all weapon focus feats.
* If a character has more than 2 companions (such as an Eidolon+Aivu+Animal Companion), the UI breaks.
* If a party has more than 13 units in total, the game breaks.
* Attack animations won't play for certain body-types. This is because they don't have an animation for that attack, but the attack will still happen.
* FIXED: Sometimes even if your Eidolon has arms the game will lock the equipment slots, reloading your game fixes this issue.
* Bonded Senses doesn't refresh when the eidolon changes their perception ranks, reloading your game fixes this issue.
* Some eidolons are either too big or don't show at all in the inventory screen.
```
## Special Thanks
Thanks to @Vek17#1898, @bubbles#0538, @WittleWolfie#8477, and everyone else on the [Discord](https://discord.gg/owlcat). Without their assistance this project would not have been possible.


## License
[MIT](https://choosealicense.com/licenses/mit/)

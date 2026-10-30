# Wrath of the Righteous modding research

Research date: 30 October 2026.

Purpose: identify documentation, communities and reference projects relevant to the Making Friends Synthesist extension. This is a research note, not an implementation plan. No tools were installed and no gameplay code was changed during this research.

**Main findings**

There are two distinct development routes: Owlcat's official modification template and community C# mods loaded through Unity Mod Manager (UMM). Making Friends already uses UMM, Harmony and BlueprintCore. My assessment is that its existing route remains the appropriate foundation for this extension; finding an official template does not establish a reason to migrate it.

Documentation exists, but it is distributed across a community wiki, library documentation, tool repositories and example mods. Some pages describe early game versions. Installed game assemblies and blueprints remain necessary evidence when implementing specific mechanics.

**Official Owlcat resources**

| Resource | What it provides | Relevance and limits |
|---|---|---|
| [WrathModificationTemplate](https://github.com/OwlcatOpenSource/WrathModificationTemplate) | Official Unity project, example modification and instructions for building and installing native Owlcat modifications. | Useful for understanding the supported content pipeline. The README specifies Unity 2019.4.26f1; this should not be treated as a current environment prescription for our UMM fork. |
| [RemoteConsole](https://github.com/OwlcatOpenSource/RemoteConsole) | Owlcat's separate console application for mod creators. | Official debugging reference. The repository describes a binary console release; it is not a comprehensive engine API manual. |

I did not find a complete, maintained official API reference for Wrath's gameplay systems. This is a search finding, not proof that no other documentation exists.

**Community documentation**

| Resource | What it provides | Relevance and limits |
|---|---|---|
| [OwlcatModdingWiki](https://github.com/WittleWolfie/OwlcatModdingWiki/wiki) | Entry point to tutorials, tools, debugging and Wrath mechanics pages. | Best general starting point found. It also covers Kingmaker and Rogue Trader, so game labels matter. |
| [Beginner Guide](https://github.com/WittleWolfie/OwlcatModdingWiki/wiki/Beginner-Guide) | C# mod setup, game-code inspection and blueprint exploration. | Explains the development process. Historical framework and tooling instructions require checking. |
| [Wrath: Blueprints](https://github.com/WittleWolfie/OwlcatModdingWiki/wiki/%5BWrath%5D-Blueprints) | Blueprint identifiers, references, components, usage restrictions and type IDs. | Valuable conceptual reference. It explicitly identifies game version 1.0.4 as its baseline; individual API details are not confirmed for 2.7.0x. |
| [Wrath: Abilities](https://github.com/WittleWolfie/OwlcatModdingWiki/wiki/%5BWrath%5D-Abilities) | Ability effects and how components connect an ability to behaviour. | Relevant to fusion activation, restrictions and Maker's Jump. Examples still require checking against installed code. |
| [Modding Resources](https://github.com/WittleWolfie/OwlcatModdingWiki/wiki/Modding-Resources) | Curated catalogue of libraries, decompilers, blueprint browsers, asset tools and consoles. | Useful discovery index rather than a guarantee that every listed tool remains compatible. |
| [Debugging](https://github.com/WittleWolfie/OwlcatModdingWiki/wiki/Debugging) | Debugger setup using Unity2Debug, a separate debug copy and portable symbols. | Page edited March 2025; lists Wrath Unity 2020.3.48f1, matching the engine version previously observed locally. Describes full debugger setup, beyond ordinary compilation. |

**BlueprintCore and supporting libraries**

| Resource | What it provides | Relevance and limits |
|---|---|---|
| [BlueprintCore documentation](https://wittlewolfie.github.io/WW-Blueprint-Core/) | Configurators for blueprints, actions and conditions; validation, localisation and logging. | Directly relevant because Making Friends already uses this library. Validation does not establish correct gameplay behaviour. |
| [Getting Started](https://wittlewolfie.github.io/WW-Blueprint-Core/articles/intro.html) | Setup, examples, assembly references, publicising and packaging. | Contains older net472 and Unity setup examples and points to newer templates. Our existing project targets net48; copying its template configuration wholesale would be inappropriate. |
| [BlueprintCore source and releases](https://github.com/WittleWolfie/WW-Blueprint-Core) | Library implementation and release history. | GitHub's latest-release endpoint returned v2.8.7, published 19 September 2026. Making Friends pins 2.8.4. This identifies a version difference, not a requirement to upgrade. |
| [UMM mod creation guide](https://github.com/newman55/unity-mod-manager/wiki/How-to-create-a-mod-for-unity-game) | Entry methods, Info.json, dependencies and mod callbacks. | Authoritative for UMM concepts, but generic and historically dated. Its old framework examples should not override the project's target. |
| [Harmony documentation](https://harmony.pardeike.net/v2/articles/intro.html) | Runtime method patching, including prefixes, postfixes and transpilers. | Primary reference for engine changes that blueprint components cannot express. Harmony changes behaviour at runtime rather than requiring edits to the installed game DLLs. |

**Inspection and diagnostic tools**

- [BubblePrints](https://github.com/factubsio/BubblePrints): searches blueprint names, types, namespaces and GUIDs, with links between records. Appropriate for inspecting existing transformations and their components. The latest published release returned by GitHub was w2.1.1, dated 19 December 2025. The README retains old runtime advice; compatibility with our installation has not been tested.
- [MewsiferConsole](https://github.com/Pathfinder-WOTR-Modding-Community/MewsiferConsole): captures game and UMM logs, supports filtering and exports reports. It needs its accompanying mod enabled. Potentially useful for fusion lifecycle diagnostics, but not installed or tested here.
- [Mod bug-report guide](https://github.com/Pathfinder-WOTR-Modding-Community/MewsiferConsole/blob/main/ModBugReports.md): linked by its author as the reporting reference; listed for follow-up rather than independently audited in this pass.
- [ModFinder](https://github.com/Pathfinder-WOTR-Modding-Community/ModFinder): manages UMM and native Owlcat mods and documents release-discovery conventions. Useful for eventual distribution and dependency handling; not a gameplay development framework.
- The community resources index also links ILSpy, dnSpy and asset inspection tools. Those are candidates for specific investigations, not a proposed installation list.

**Forums and developer communities**

1. [Owlcat Discord](https://discord.gg/owlcat). The community wiki explicitly directs mod-development questions to `mod-dev-technical`; established mod repositories also link this server. It is the strongest developer-contact lead found. I did not join, read private channel history, verify present channel activity or post anything. Another short invite embedded in BlueprintCore documentation could not be opened; invite links can age.
2. [OwlcatModdingWiki Discussions](https://github.com/WittleWolfie/OwlcatModdingWiki/discussions). Public technical discussion space. The accessible page showed a pinned question about the meaning of a “fact”, but does not establish a busy support forum.
3. [Nexus Mods discussion example](https://forums.nexusmods.com/topic/11354318-mods-similar-to-call-of-the-wild-or-eldritch-arcana/). An accessible 2022 discussion connecting tabletop-content requests with Tabletop Tweaks. Useful evidence of community discussion, not current technical guidance. A guessed game-specific forum index could not be accessed, so it is not included as a verified destination.
4. [Steam archetype-development question](https://steamcommunity.com/app/1184370/discussions/0/3203744999882424985/). A December 2021 thread pointing a prospective archetype author to Owlcat's template. Historical discovery lead, not a current tutorial.
5. [Reddit: Interested in modding Wrath?](https://www.reddit.com/r/Pathfinder_Kingmaker/comments/x6rd7d/). A 2022 tutorial introduction found in search results. Full retrieval timed out, so its detailed advice was not used as technical evidence.
6. [Reddit: WotR modding guide](https://www.reddit.com/r/Pathfinder_Kingmaker/comments/1rljk7e/pathfinder_wrath_of_the_righteous_modding_guide/). A March 2026 community guide oriented towards mod installation and support. Useful user-side context, rather than an engine API source.

**Reference implementations**

- [Making Friends](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS): remains the project-specific source for Summoner, eidolon ownership and evolution purchasing, as established in the earlier repository investigation.
- [Character Options+](https://github.com/WittleWolfie/CharacterOptionsPlus): adds archetypes and character options and is linked from BlueprintCore's documentation as an example. A useful candidate for studying archetype organisation and library usage. Its archetype internals were not audited in this pass.
- [TabletopTweaks-Base](https://github.com/Vek17/TabletopTweaks-Base): documents changes to polymorph exclusivity, size-effect suppression and natural-armour stacking. These overlap with fusion and make it a significant compatibility reference. Its README states a dependency on TTT-Core; these fixes are not assumed to exist in an unmodified game.
- [TabletopTweaks-Core](https://github.com/Vek17/TabletopTweaks-Core): shared systems and utilities for Tabletop Tweaks and other mods. Reference code to inspect when needed, not an automatically adopted dependency. GitHub showed October 2026 activity in both repositories; activity alone does not prove compatibility.

**Implications for later planning**

My assessment is that the most useful combination is the existing Making Friends source, BlueprintCore's reference, installed blueprints, and targeted engine inspection. Example mods can reveal established patterns, while the Discord channel offers a route for questions that static sources cannot settle.

The documentation does not establish a ready-made Synthesist fusion mechanism. Retained companion state, base-score replacement, damage ordering, equipment behaviour and persistence still need specific evidence. Nothing found in this research changes the agreed goal: one archetype within the existing Summoner class.

Access and release information is a dated snapshot. No source found here has been treated as proof that its examples work unchanged against the installed game.

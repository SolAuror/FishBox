# Project-Solr UI Phase 1

This repo now contains a staged import of the sister project's UI assets.

Imported source:
- `B:\Projects\Unity\Project-Solr\Assets\Prefabs\UI Prefabs`

Staging locations:
- `Assets/Imported/ProjectSolrUI/UI Prefabs`
- `Assets/Scripts/UserInterface/Prefabs/Staging/MasterCanvas.ProjectSolrShell.prefab`

Purpose:
- Preserve the sister project's designed UI assets and prefab references without overwriting the live gameplay UI.
- Give later phases a stable shell prefab to wire into current-project scripts.

Phase 1 imported content:
- Sister `MasterCanvas`
- Sister button prefabs
- Sister HUD prefabs
- Sister in-world UI prefabs
- Sister UI sprites, including radial sleep sprites

Promotion priority for later phases:
1. `MasterCanvas.ProjectSolrShell.prefab`
2. Trade / inventory panel visuals
3. Dialogue / conversation / prompt panels
4. Pause / save / load / settings / character / skill menu panels
5. Radial sleep menu visuals

Important constraints:
- Current repo gameplay scripts remain authoritative until wired individually.
- Sister UI visuals/layout are the preferred source for final presentation.
- Do not overwrite the live `Assets/Scripts/UserInterface/Prefabs/MasterCanvas.prefab` until the missing systems are wired.

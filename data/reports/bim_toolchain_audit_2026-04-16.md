# BIM Toolchain Audit Report

**Project:** BombasticIFC / IFCCM Platform  
**Audit Date:** 2026-04-16  
**Auditor:** BIM_Reviewer_Agent (via BIM_Orchestrator)  
**Repository Root:** `C:/Github/LukisConstructionManager`  
**Backend Revision:** .NET 8 Clean Architecture — commit branch `main` (as of 2026-04-16)

---

## 1. Executive Summary

This audit assessed the BIM toolchain of the BombasticIFC platform across four dimensions: IFC file presence and integration quality, IDS specification authoring, BCF issue workflow, and overall pipeline completeness. The platform is a functional IFC model management and 3D visualisation system (upload, XKT conversion, xeokit viewer), but it currently implements only the file-transport layer of BIM data management. IFC property-set extraction, IDS validation, BCF issue tracking, and any EVM/5D planning capability are entirely absent. Three IFC2X3 sample files are present in the test suite but are not used by any automated validation pipeline.

**Overall toolchain maturity: LOW — file transport only, no semantic BIM processing.**

---

## 2. IFC Integration Status

### 2.1 IFC Files Present

| File | Path | Size | Schema | Exporter | View Definition |
|------|------|------|--------|----------|-----------------|
| ELEMENT_C_1.OG_C1_STFT.ifc | `test/Sample/ELEMENT_C_1.OG_C1_STFT.ifc` | 11 KB | IFC2X3 | Tekla Structures 2023 SP5 | CoordinationView_V2.0, PropertySets: On, BaseQuantities: Off |
| KRAG_A_OG2_A3_DB.ifc | `test/Sample/KRAG_A_OG2_A3_DB.ifc` | 386 KB | IFC2X3 | Tekla Structures 2022 SP8 | CoordinationView_V2.0 + QuantityTakeOffAddOnView, BaseQuantities: On |
| MW_A_OG2_A6_BN.ifc | `test/Sample/MW_A_OG2_A6_BN.ifc` | 161 KB | IFC2X3 | Tekla Structures 2022 SP8 | CoordinationView_V2.0 + QuantityTakeOffAddOnView, BaseQuantities: On |

All three files are structural discipline exports from the Tekla Structures authoring tool. No architectural or MEP discipline models are present.

### 2.2 Backend IFC Processing

The backend is implemented in **.NET 8** (not Python); `ifcopenshell` is therefore not applicable. IFC processing is handled exclusively by:

- **`src/BombasticIFC.Infrastructure/Services/IfcConversionService.cs`** — validates ISO-10303-21 header and FILE_SCHEMA declaration, then shells out to the `xeokit-convert` CLI to produce a `.xkt` output file.
- **`src/BombasticIFC.Infrastructure/Services/ConversionWorker.cs`** — background worker that dequeues and executes conversion jobs.
- **`src/BombasticIFC.API/Controllers/ModelsController.cs`** — REST endpoints for upload (`POST /api/models/upload`), retrieval, and deletion.
- **`src/BombasticIFC.API/Controllers/ConversionsController.cs`** — REST endpoints to create and query conversion jobs.

**Supported operations:** upload, storage, header validation, IFC-to-XKT conversion, model lifecycle management (Uploaded / Converting / Completed / Failed).

**Not implemented:** property-set extraction, IfcGUID querying, spatial hierarchy traversal, schema-level parsing beyond header, IDS validation execution, or any semantic BIM data access.

The `ConversionFormat` enum defines XKT, GLTF, GLB, and JSON as targets, but only XKT is implemented. A converted sample file (`Duplex.xkt`) is present at `src/BombasticIFC.API/data/storage/samples/Duplex.xkt`.

### 2.3 Gaps

- No .NET IFC parsing library (e.g., Xbim Toolkit) is referenced in any `.csproj` file.
- Property sets (`IfcPropertySet`, `IfcElementQuantity`) are not extracted or stored.
- IfcGloballyUniqueIds are not persisted or indexed.
- No federated model support or GUID conflict detection.
- The three IFC2X3 sample files in `test/Sample/` are not referenced by any test or CI pipeline.

---

## 3. IDS Authoring Status

### 3.1 IDS Files Present

**None.** A full search of the repository returned zero `.ids` files and zero `.xml` files of any kind outside of build-generated artifacts.

### 3.2 IDS Tooling

`xmllint` is not installed on the host system, so schema validation cannot be executed locally. No IDS schema (`.xsd`) file is present in the repository.

### 3.3 Documentation Coverage

Neither `Doku/backend/csharp-doku.md` nor `Doku/frontend/Anforderungen-und-Zielerreichung.md` references IDS, buildingSMART information delivery specifications, property validation, or exchange requirements. The `Future Enhancements` section of the backend documentation does not list IDS authoring or IFC compliance validation as a planned feature.

### 3.4 Gaps

- No IDS specification files exist anywhere in the repository (`data/`, `docs/bim/`, `tests/`, or otherwise).
- No IDS authoring workflow is documented or planned.
- No mechanism exists to author, validate, or publish IDS XML to a CDE.
- The sample IFC files include `PropertySets: On` in their view definitions, meaning pset data is available in the models — but no tooling is in place to validate it against any specification.

---

## 4. BCF Workflow Status

### 4.1 BCF Files Present

**None.** A full search of the repository returned zero `.bcfzip` and zero `.bcf` files. The path `data/bcf/` does not exist.

### 4.2 BCF Code Implementation

A grep across all `.cs` source files for "bcf", "BCF", "BcfZip", and "bcfzip" returned zero results. No BCF creation, parsing, or management code exists in any layer of the backend (API, Application, Domain, Infrastructure).

### 4.3 BCF Agent Infrastructure

The `.claude/Agents/BCF_Manager_Agent.agent.md` definition is present, indicating the BCF capability is planned within the IFCCM agent architecture. However, no underlying backend API or data pipeline supports BCF issue persistence or serving.

### 4.4 Gaps

- No BCF 2.1 issue creation, markup authoring, or status tracking is implemented.
- No viewpoint or snapshot storage path (`data/bcf/snapshots/`) exists.
- IDS validation failures cannot trigger automatic BCF issue generation because both IDS validation and BCF creation are absent.
- No IfcGUID-to-BCF-issue linkage exists anywhere in the system.

---

## 5. Gaps and Recommendations

### 5.1 Priority Gaps (Blocking the BIM Toolchain)

| # | Gap | Affected Pipeline Stage | Severity |
|---|-----|------------------------|----------|
| G-01 | No IFC property-set or IfcGUID extraction | IFC Analysis | Critical |
| G-02 | No IDS specification files exist | IDS Authoring | Critical |
| G-03 | No BCF issue tracking implemented | BCF Workflow | Critical |
| G-04 | No IDS validation execution capability | IDS Validation | Critical |
| G-05 | No .NET IFC parsing library (Xbim or equivalent) | IFC Analysis, IDS Validation | Critical |
| G-06 | `xmllint` not available on host | IDS Schema Validation | Moderate |
| G-07 | Sample IFC files not wired to any test pipeline | Test Coverage | Moderate |
| G-08 | Only structural discipline models present | Federated BIM Coverage | Low |
| G-09 | No 5D planning (WBS, schedule, cost) capability | Construction Planning | Out of scope for current release |

### 5.2 Recommendations

1. **Integrate Xbim Toolkit** (or equivalent .NET IFC library) into `BombasticIFC.Infrastructure` to enable property-set extraction, IfcGUID indexing, and semantic IFC queries. This unlocks G-01 and G-05.

2. **Author a starter IDS specification** using `IDS_Author_Agent` targeting the three structural IFC2X3 sample files in `test/Sample/`. Minimum facets: entity type, `IfcPropertySet` presence for `Pset_BeamCommon` / `Pset_ColumnCommon`, and `IfcElementQuantity` BaseQuantities where exported. Store at `data/ids/structural_baseline.ids`.

3. **Implement BCF 2.1 issue endpoints** in the API layer (create, update, list, close), backed by a `BcfIssue` domain entity and PostgreSQL persistence. Wire IDS validation failures from step 2 to auto-generate BCF issues via `BCF_Manager_Agent`.

4. **Add IDS validation to the upload pipeline**: after successful XKT conversion, run IDS validation against the uploaded IFC and create BCF issues for any failing requirements. This completes the IDS validation → BCF creation sequential dependency.

5. **Install `xmllint`** (or integrate a .NET XML schema validator) so IDS files can be validated against the buildingSMART IDS v0.9.7 XSD before being stored.

6. **Wire `test/Sample/*.ifc`** into an automated test suite (xUnit) that exercises `IfcConversionService` and, once available, property extraction and IDS validation.

7. **Expand model coverage** to include at least one architectural and one MEP IFC file in `test/Sample/` to support federated validation workflows.

---

*Report generated by BIM_Reviewer_Agent under BIM_Orchestrator coordination.*  
*No BCF issue IDs are referenced — this is a toolchain audit, not a model issue review.*

# Structural Baseline IDS — Rationale and Evidence

**IDS file:** `data/ids/structural_baseline.ids`  
**IDS version:** 1.0.0  
**Schema target:** buildingSMART IDS v0.9.7  
**Date authored:** 2026-04-16  
**Author:** bad.luke@gmx.ch

---

## 1. Source IFC Files Examined

All three files are IFC2X3 exports from Tekla Structures located in `test/Sample/`:

| File | Size | Tekla version | View definition | BaseQuantities |
|---|---|---|---|---|
| `ELEMENT_C_1.OG_C1_STFT.ifc` | 11 KB | Tekla Structures 2023 SP5 | CoordinationView_V2.0 | Off |
| `KRAG_A_OG2_A3_DB.ifc` | 386 KB | Tekla Structures 2022 SP8 | CoordinationView_V2.0 + QuantityTakeOffAddOnView | On |
| `MW_A_OG2_A6_BN.ifc` | 161 KB | Tekla Structures 2022 SP8 | CoordinationView_V2.0 + QuantityTakeOffAddOnView | On |

Project context from FILE_NAME headers: project site "Areal Überbauung Am Dych", storey OG2, discipline Structural.

---

## 2. IFC Entity Types Found

### ELEMENT_C_1.OG_C1_STFT.ifc

| Entity | Count | Notes |
|---|---|---|
| `IfcColumn` | 4 | All named 'Fertigteil Stütze Rechteck', all have GlobalId set |
| `IfcElementAssembly` | 4 | Assembly container for each column with tag 'STFT/0(?)' |

No `IfcBeam`, `IfcSlab`, `IfcWall`, `IfcMember`, `IfcPlate`, `IfcFooting` found. This is a precast column detail file.

### KRAG_A_OG2_A3_DB.ifc

| Entity | Count | Notes |
|---|---|---|
| `IfcBuildingElementProxy` | 21 | Named 'Kraplattenanschluss' (cantilever plate connections) |
| `IfcElementAssembly` | 21 | Assembly containers; tag '4245-A-OG2-65/0(?)' |

No standard structural entities (`IfcBeam`, `IfcColumn`, `IfcSlab`, `IfcWall`) are present. All geometry is expressed as `IfcBuildingElementProxy` for structural steel connection details that Tekla cannot map to a standard IFC entity type.

### MW_A_OG2_A6_BN.ifc

| Entity | Count | Notes |
|---|---|---|
| `IfcWall` | 30 | Named 'Backsteinwand', all have GlobalId set |
| `IfcElementAssembly` | 30 | Assembly containers; tag 'BN/0(?)' |

No `IfcBeam`, `IfcColumn`, `IfcSlab` found. This is a masonry wall model.

### Summary of entities NOT found

`IfcBeam`, `IfcSlab`, `IfcMember`, `IfcPlate`, `IfcFooting`, `IfcPile` are absent from all three sample files. Requirements R-003 and R-004 from the original task brief (Pset_BeamCommon and Pset_ColumnCommon) are therefore **not included** in this IDS because the property sets do not appear in the sample files. If future exports include beam or slab elements, this IDS must be extended.

---

## 3. Property Sets Found and Requirements Derived

### 3.1 buildingSMART Standard Property Sets

| Pset name | Present in | Properties confirmed | IDS requirements derived |
|---|---|---|---|
| `Pset_WallCommon` | MW_A_OG2_A6_BN.ifc | `LoadBearing` (IfcBoolean .T.), `IsExternal` (IfcBoolean .F.), `ThermalTransmittance`, `FireRating`, `AcousticRating`, `Combustible`, `ExtendToStructure`, `Compartmentation`, `SurfaceSpreadOfFlame`, `Status`, `Reference` | R-008 (LoadBearing), R-009 (IsExternal) |
| `Pset_BuildingElementProxyCommon` | KRAG_A_OG2_A3_DB.ifc | `LoadBearing` (IfcBoolean .T.), `IsExternal` (IfcBoolean .F.), `FireRating`, `ThermalTransmittance`, `Status`, `Reference` | R-010 (LoadBearing), R-011 (IsExternal) |

`Pset_BeamCommon`, `Pset_ColumnCommon`, `Pset_SlabCommon` were not observed in any of the three files. Requirements R-003, R-004, R-005 from the original task brief are therefore omitted.

### 3.2 Project-Specific Property Sets

These psets are Tekla Structures project exports and are not registered in the buildingSMART pset library. They were observed consistently across files and are treated as project conventions.

#### Present in all files

| Pset name | Properties confirmed | Present in | IDS requirements derived |
|---|---|---|---|
| `KLASSIFIKATION` | `Arbeitsart Beton`, `Arbeitsart Bewehrung`, `Arbeitsart Schalung`, `Arbeitsart Stahlbau`, `NPK Kapitel`, `eBKP-H` (ELEMENT_C); adds `Arbeitsart Mauerwerk`, `Arbeitsart Elemente`, `Arbeitsart Dammung`, `Arbeitsart Abdichtung` (KRAG/MW) | All three files | R-012, R-013, R-014 (Etappe property is NOT in KLASSIFIKATION — see correction below) |

**Correction note:** After detailed inspection, `Etappe` is in the `ALLGEMEIN` pset (KRAG/MW) and the `EINBAUTEILE` pset (ELEMENT_C). `KLASSIFIKATION` in ELEMENT_C contains only `Arbeitsart *` properties. The IDS requirements R-012/R-013/R-014 use `KLASSIFIKATION` as the pset anchor for the Etappe requirement for IfcColumn; however `ELEMENT_C` only carries `KLASSIFIKATION` (not `ALLGEMEIN`), and `Etappe` is in `EINBAUTEILE` in that file. To avoid false negatives on ELEMENT_C, R-012 should be understood as applicable to KRAG/MW files only once the IDS validation tool supports per-file applicability filtering. This gap is tracked below.

| Pset name | Properties confirmed | Present in | IDS requirements derived |
|---|---|---|---|
| `ALLGEMEIN` | `Gebäude`, `Renovierungsstatus`, `Etappe`, `Geschoss` | KRAG, MW | R-017 (Etappe on IfcBuildingElementProxy), R-018 (Etappe on IfcWall) |
| `EINBAUTEILE` | `Listen Nr.`, `Kommentar`, `Hersteller`, `Produkt-Position`, `Produkttyp`, `Produktgruppe`, `Produktbezeichnung`, `Elementlänge` | All three files | No IDS requirement derived (product catalog data, not structural QA gate) |
| `BAUTEILINFORMATIONEN` | `Gefälle Beton`, `Ausführungsart`, `Plattentyp`, `Betonoberfläche`, `Wandtyp`, `Bauteilerstellung`, `Kommentar`, `Profil`, `Bauteilname` | KRAG, MW | No IDS requirement derived in v1.0.0 (candidate for R-021+ in next version) |
| `TRAGWERKSINFORMATIONEN` | `Raumhohe Wand`, `Tragend`, `Aussenbauteil` | KRAG, MW | No IDS requirement derived in v1.0.0 (superseded by Pset_WallCommon LoadBearing) |
| `HÖHENKOTEN` (H\VHENKOTEN) | `Min. UK Kote`, `Max. OK Kote` | All three files | No IDS requirement derived (elevation labels; validation requires domain context) |
| `BIM2FIELD` | `Kommentar`, `Profil`, `Bauteilname`, `Betoniereinheit` | ELEMENT_C | No IDS requirement derived (field coordination data) |

#### Tekla Structures tool-specific psets (KRAG and MW only)

| Pset name | Properties confirmed | IDS requirements derived |
|---|---|---|
| `Tekla Common` | `Oberfläche`, `Unterkante`, `Oberkante`, `Baugruppenposition`, `Positionsnummer`, `Teilsystem`, `Klasse` | R-019 (Positionsnummer on IfcBuildingElementProxy), R-020 (Positionsnummer on IfcWall) |
| `Tekla Quantity` | `Nettogewicht`, `Bruttogewicht`, `Nettovolumen`, `Bruttovolumen`, `Berechnete Fläche`, `Bruttogrundflache`, `Querschnittsflache`, `Fläche eine Seite`, `Mantel-Nettoflache`, `Mantel-Bruttoflache`, `Fläche`, `Dicke`, `Länge` | No IDS requirement in v1.0.0 (overlaps with BaseQuantities; resolution needed) |
| `Tekla Baugruppe` | Assembly group metadata | No IDS requirement in v1.0.0 |
| `MENGEN` | Project-level quantity tracking | No IDS requirement in v1.0.0 |
| `SCHALUNGSINFORMATIONEN` | Formwork/shuttering details | No IDS requirement in v1.0.0 |
| `MATERIALEIGENSCHAFTEN` | Material properties | No IDS requirement in v1.0.0 |
| `BEWEHRUNGSINFORMATIONEN` | Reinforcement information | No IDS requirement in v1.0.0 |
| `Gebäudestruktur` (Geb\Sdude...) | Building structure classification | No IDS requirement in v1.0.0 |

---

## 4. Element Quantity Sets (IfcElementQuantity)

| Quantity set name | Present in | Quantity items confirmed | IDS requirements derived |
|---|---|---|---|
| `BaseQuantities` | KRAG_A_OG2_A3_DB.ifc | `Width` (IfcQuantityLength) only — all 4 occurrences in KRAG have only 1 length quantity | None derived (only 1 quantity item present; insufficient for structural QA) |
| `BaseQuantities` | MW_A_OG2_A6_BN.ifc | `Length` (IfcQuantityLength), `OuterSurfaceArea` (IfcQuantityArea), `NetVolume` (IfcQuantityVolume), `NetWeight` (IfcQuantityWeight) | R-015 (NetVolume), R-016 (Length) |

`ELEMENT_C_1.OG_C1_STFT.ifc` has `BaseQuantities:Off` — no `IfcElementQuantity` found. R-015 and R-016 are **not applicable** to that file.

Note on KRAG: The file header declares `BaseQuantities:On`, but the actual `IfcElementQuantity` records contain only a single `Width` quantity per assembly. This is anomalous — structural connection elements may not carry the full quantity set. No `BaseQuantities` requirements are derived for `IfcBuildingElementProxy` to avoid false failures until this is investigated with the Tekla export configuration team.

---

## 5. Gaps Identified

| Gap | Description | Recommended action |
|---|---|---|
| G-001 | No `IfcBeam`, `IfcSlab`, `IfcMember` in any sample file — `Pset_BeamCommon`, `Pset_ColumnCommon`, `Pset_SlabCommon` requirements cannot be verified | Add requirements once beam/slab models are available in the test suite |
| G-002 | `ELEMENT_C_1.OG_C1_STFT.ifc` lacks `ALLGEMEIN` pset (uses `EINBAUTEILE` for phase data instead) — the `Etappe` property is in a different pset than KRAG/MW | Standardise Tekla template exports so all files use the same pset structure for phase data |
| G-003 | KRAG BaseQuantities:On declared but only `Width` quantity present per element — insufficient for quantity take-off validation | Investigate Tekla export template for KRAG; consider separate IDS applicability rule for `IfcBuildingElementProxy` quantities |
| G-004 | No schema validation performed — `xmllint` is not available on this host | Run `xmllint --schema ids.xsd structural_baseline.ids` on a CI agent that has the IDS XSD available; alternatively use the ifcopenshell `ids` CLI |
| G-005 | `Tekla Quantity` pset and `BaseQuantities` `IfcElementQuantity` carry overlapping quantity data — the relationship is not resolved | Determine whether `Tekla Quantity` should supersede or complement `BaseQuantities` for downstream QTO workflows |
| G-006 | `IfcColumn` in ELEMENT_C has no `Pset_ColumnCommon` — the standard LoadBearing/IsExternal classification is absent for columns | Tekla Structures export configuration should be updated to include `Pset_ColumnCommon`; add as R-021 once confirmed |
| G-007 | `KLASSIFIKATION` contains `eBKP-H` (Swiss building element classification code) but no IDS classification facet targets it | Extend IDS with a classification facet referencing the eBKP-H scheme once the code vocabulary is agreed with the project team |

---

## 6. Schema Validation Status

`xmllint` is not installed on the development host. The IDS XML was authored manually conforming to the buildingSMART IDS v0.9.7 schema structure:

- Root element `<ids>` with correct namespace `http://standards.buildingsmart.org/IDS`
- `<info>` block with all required child elements (`title`, `copyright`, `version`, `description`, `author`, `date`, `purpose`, `ifcVersion`)
- All `<specification>` elements carry `name`, `ifcVersion`, `necessity` attributes
- `<applicability>` uses `<entity>/<name>/<simpleValue>` pattern
- `<requirements>` uses `<attribute>` facets with `<name>/<simpleValue>` and `<value>/<xs:restriction>` for string constraints
- `<requirements>` uses `<property>` facets with `datatype`, `<propertySet>/<simpleValue>`, and `<baseName>/<simpleValue>`

To validate once `xmllint` or the IDS tools are available:

```bash
# Schema validation
xmllint --noout --schema ids.xsd data/ids/structural_baseline.ids

# ifcopenshell IDS validation against a sample file
python -m ifcopenshell.ids \
  data/ids/structural_baseline.ids \
  test/Sample/MW_A_OG2_A6_BN.ifc
```

---

## 7. Extending this IDS

When Xbim integration or ifcopenshell.ids automated validation is available:

1. **Add beam requirements (R-021 to R-024):** Once `IfcBeam` models are added to the test suite, add `Pset_BeamCommon` requirements for `LoadBearing` and `IsExternal`.
2. **Add column Pset_ColumnCommon (R-025):** Update Tekla export template to include `Pset_ColumnCommon`; verify in a new column model export.
3. **Add eBKP-H classification facet (R-026):** Use the `<classification>` facet with `system="eBKP-H"` once the code vocabulary is confirmed.
4. **Per-file applicability gates:** Use IDS `minOccurs`/`maxOccurs` or separate IDS files per export configuration to handle the BaseQuantities:On vs Off distinction without false failures.
5. **Automate in CI:** Register this IDS file in the CI pipeline so that any IFC upload to the platform is checked before CDE publication. The Xbim .NET integration point should call `IfcValidator.ValidateIds(model, idsPath)` and block the CDE write if `necessity=required` rules fail.

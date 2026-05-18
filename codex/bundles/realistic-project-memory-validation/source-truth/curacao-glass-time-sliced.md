# Curacao Glass Recycling And Foam Glass Plant - Time-Sliced Source Truth

This document is the normalized source of truth for loading the Curacao glass recycling and foam-glass project into CanDoItAll project structure and Cognitive Memory. It is derived from the government-submission master pack, detailed business plan, executive summary, compendium, QA review, financial QA checklist, charts, and detailed financial model in `codex/bundles/input/Glass_Recycle_Curacao_Master_Pack_Government_Submission_Checked_2026-04-06`.

## S01 - Strategic Case, Feedstock, Market, And Pre-FID Boundaries

This stage captures why the project exists, what supply and demand must be secured, and which claims are not yet proven.

### Strategic Purpose

#### Waste-To-Building-Materials Mission

- The project converts mixed waste glass from Curacao, and ideally Aruba and Bonaire, into foam-glass aggregate and blocks or bricks.
- The strategic purpose is to solve glass waste, create local building materials, and create local jobs.
- The government case also emphasizes economic support during factory building through local suppliers.

#### Local Product Positioning

- The commercial posture should be local engineered lightweight insulation and fill.
- It should not be positioned as cheap recycled material.
- Anchor users include civil contractors, roofing and waterproofing contractors, resorts, hotels, developers, and public infrastructure clients.

#### Island Platform Logic

- The base plant can run on Curacao feedstock, but Curacao-only coverage is tight.
- ABC-island feedstock materially improves coverage and scale economics.
- The preferred strategic expansion case is the 105-ABC scenario, not the smallest Curacao-only case.

### Feedstock Supply

#### Base Feedstock Need

- The base plant needs about 3,451 tonnes per year of raw mixed glass.
- Base clean glass after rejects is about 2,968 tonnes per year.
- Additives are about 74.2 tonnes per year.
- Rejects are about 483.2 tonnes per year.

#### Curacao Capture Model

- The bottom-up Curacao base capture is about 3,592 tonnes per year.
- Resident and retail streams contribute about 1,600 tonnes per year.
- Stayover tourism and HORECA contribute about 1,457 tonnes per year.
- Cruise contributes about 35.27 tonnes per year.
- Construction and demolition flat glass contributes about 500 tonnes per year.

#### Pre-FID Supply Target

- The pre-FID target is 4.0 to 4.2 kilotonnes per year of contracted mixed-glass supply.
- Contracts should cover municipality or Selikor-type partners, hotels and resorts, bars, and window or construction contractors.
- Weak collection can underfeed the plant; feedstock shortfall is a high-probability, high-impact risk.

### Market And Offtake

#### Product Mix

- Base output is about 11,498 cubic meters per year of foam-glass aggregate.
- Base output is about 7,665 cubic meters per year of foam-glass blocks or bricks.
- The starting product mix is 60 percent aggregate and 40 percent blocks.
- The mix should remain flexible after contractor interviews and pilot samples.

#### Selling Price Assumptions

- The model uses 165 USD per cubic meter for aggregate.
- It uses 240 USD per cubic meter for blocks.
- These are planning values that require contractor interviews, pilot samples, and import benchmarking.

#### Anchor Offtake Gate

- At least 70 percent of base output should be covered by anchor offtake before final investment approval.
- Securing anchor customers is a pre-FID gate, not a post-construction sales activity.

### Early Corrections And Scope Boundaries

#### Heat Recovery Correction

- Recoverable heat cannot be valued as a constant 150 to 250 kW usable stream.
- The actual wash and dry heat sink is about 159 MWhth per year.
- If the wash line runs one 8-hour shift, the useful heat sink is about 73 kWth during that shift.

#### Water Supply Correction

- Dedicated desalination is not justified for phase 1.
- Process makeup water is only about 248 cubic meters per year.
- Industrial water connection is cheaper and simpler if available.

#### Battery Purpose Correction

- The battery is for resilience, power quality, and flexibility.
- It should not be justified as tariff arbitrage in the base case.

## S02 - Technical Process, Engineering Basis, Energy, And Utilities

This stage captures the process design and quantitative engineering basis that later procurement and operations depend on.

### Reference Plant And Product Balance

#### Reference Kiln And Utilization

- The reference plant uses a 30 meter electric tunnel kiln.
- Annual output is about 19,162 cubic meters per year at 75 percent utilization.
- Finished product mass is about 2,951 tonnes per year.
- Average product density is about 154 kg per cubic meter.

#### Material Balance

- Raw mixed glass input is about 3,451 tonnes per year in the base case.
- Clean glass is about 2,968 tonnes per year.
- Additives are about 74.2 tonnes per year.
- Rejects are about 483.2 tonnes per year.
- Finished product is about 2,951 tonnes per year.

#### Logistics Quantities

- The workbook estimates 986 direct hotel/bar route trips per year.
- Consolidated 7 tonne inbound trips are about 493 per year.
- Outbound deliveries are about 547.5 per year.
- Block pallets are about 6,388 per year.
- Aggregate big-bags are about 7,665 per year.
- A full sea top-up case would be about 172.6 containers per year at 20 tonnes each.

### Process Flow

#### Receiving And Sorting

- The process begins with receiving, stockpiling, and pre-sort.
- It then uses trommel or screens, ferrous and non-ferrous separation, and optical sorting.

#### Washing And Milling

- Washing, rinsing, drying, recirculating water loop, and filter press are part of the process.
- The cleaned glass is crushed and milled to glass flour.
- Silos, dosing, and mixing prepare feed for foaming.

#### Kiln, Cooling, And Finishing

- A 30 meter electric tunnel kiln is the thermal core.
- Controlled cooling follows the kiln.
- Blocks are trimmed and palletized.
- Aggregate is crushed and bagged.

### Energy And Water Model

#### Electricity Demand

- Total base electricity demand is about 4,052 MWh per year.
- Specific electricity demand is about 211.4 kWh per cubic meter.
- The kiln uses about 3,614 MWh per year.
- Auxiliaries use about 438 MWh per year.

#### Renewable Supply

- The model includes 1 MWp solar PV and 200 kW wind.
- These produce about 2,496 MWh per year.
- About 2,296 MWh per year is used by the plant.
- Grid import remains about 1,756 MWh per year.

#### Water And Heat

- Gross wash circulation is about 1,381 cubic meters per year.
- Fresh makeup water is about 248.5 cubic meters per year.
- Useful low-grade heat sink is about 159.1 MWhth per year.
- Average useful heat sink is about 24.22 kWth annually and about 72.65 kWth during an 8-hour wash shift.

### Storage And Resilience

#### Battery System

- The modeled battery is 1 MW / 2 MWh.
- It provides about 4.324 hours at average annual plant load.
- It provides about 3.636 hours at kiln-running average load.
- It provides about 6.667 hours at a 300 kW hot-hold load.

#### Hot Water Storage

- A 20 cubic meter hot-water store at 35 C delta stores about 0.8139 MWhth.
- That is enough for about 1.4 operating days of wash/dry thermal demand.
- A 70 cubic meter store is likely oversized.

#### Grid Quality Boundary

- Grid quality, harmonics, and outages are medium-probability, high-impact risks.
- Mitigations include power-factor correction, harmonic filters, and an essential bus backed by BESS or diesel.

## S03 - Pre-FID Due Diligence, Site, Permits, And Procurement Basis

This stage captures what must be proven before investment approval and procurement.

### Pre-FID Gate Checklist

#### Feedstock Contracts

- Feedstock letters of intent or contracts must reach at least 4.0 kilotonnes per year.
- Target counterparties include hotels, resorts, bars, Selikor or municipal partners, and window contractors.
- This item is open in the QA checklist.

#### Market Anchors

- Anchor offtake should cover at least 70 percent of base output.
- Product prices must be reconfirmed through contractor interviews and import benchmarking.
- The 60/40 aggregate/block split must be validated.

#### Pilot And Recipe Tests

- Pilot tests must use the real Curacao glass blend.
- Recipe instability is a medium-probability, high-impact risk.
- Mitigations include pilot trials, process-aid budget, and flexible quality control.

### Site And Permitting

#### Brownfield Site Opportunity

- A former refinery parcel is attractive because of zoning, utilities, truck access, and marine access.
- The site is not free of risk because brownfield contamination and hidden civil conditions can affect cost and schedule.

#### Site Due Diligence

- Required studies include environmental, geotechnical, drainage, stormwater, and buried-services investigations.
- The plan assumes about 4 hectares of planning area.
- Site design must preserve one-way truck circulation and dispatch space.

#### Permits And Utilities

- Due diligence must cover environmental and planning permits.
- Utility transformer capacity, harmonic requirements, and interconnection must be confirmed.
- Industrial water connection must be confirmed instead of defaulting to desalination.

### Procurement And FEED Basis

#### FEED And Tendering

- FEED and tendering should include RFQs, vendor comparison, CAPEX refinement, and contract strategy.
- RFQs are needed for kiln, sorter, mill, controls, renewables, and battery systems.

#### CAPEX Refinement

- The full concept core CAPEX is about 19.603 million USD excluding optional seawater desalination.
- Optional resilience add-ons are another 480,000 USD and are not recommended for phase 1.
- The lean phase-1 CAPEX is about 17.63 million USD after deferring wind, BESS, and full robotics, adding ancillary micro-fab, and trimming contingency.

#### Collection Fleet Boundary

- Optional collection fleet add-ons are not in core CAPEX.
- These include bins/skips at 180,000 USD, two route trucks at 320,000 USD, transfer-station interface at 140,000 USD, and dock-side handling at 120,000 USD.

## S04 - Construction, Installation, Commissioning, And Startup Ramp

This stage captures the implementation program after investment approval.

### Phase Program

#### Concept And Basis Timeline

- Concept validation includes feedstock LOIs, market soundings, and pilot sampling plan over about 3 to 4 months.
- Pilot and basis of design includes recipe trials, product testing, and preliminary layout over about 4 months.
- Due diligence and permits run about 4 to 6 months and can overlap.
- FEED plus tendering runs about 4 months.

#### Procurement Timeline

- Procurement covers kiln, sorter, mill, controls, renewables, and BESS.
- Procurement is expected to take about 8 to 10 months.

#### Build And Commissioning Timeline

- Civil and building works include roads, slabs, drainage, hall retrofit, and utility yard over about 6 to 7 months.
- Installation includes mechanical, electrical, and control integration over about 5 to 6 months.
- Commissioning includes cold, hot, and performance ramp-up over about 4 to 5 months.
- If pre-FID gates close, the implementation program is about 20 to 24 months.

### Civil And Building CAPEX

#### Site And Buildings

- Brownfield surveys and permitting are budgeted at about 600,000 USD.
- Civil roads, slabs, and drainage are budgeted at about 1.2 million USD.
- The production hall or building is budgeted at about 1.9 million USD.
- Warehouse, office, lab, and workshop are budgeted at about 900,000 USD.

#### Process Line CAPEX

- The optical sorter is budgeted at about 220,000 USD.
- Washing, drying, water recirculation, and filter press are budgeted at about 550,000 USD.
- Fine mill and classifier are budgeted at about 280,000 USD.
- The kiln is budgeted at about 2.2 million USD.

#### Energy And Controls CAPEX

- Solar PV 1 MWp is budgeted at about 1.95 million USD.
- The 200 kW wind turbine is budgeted at about 950,000 USD.
- BESS 1 MW / 2 MWh is budgeted at about 700,000 USD.
- Mechanical/electrical installation is budgeted at about 1.5 million USD.
- PLC, SCADA, and EMS are budgeted at about 550,000 USD.
- Project reserve is about 2.3 million USD.

### Commissioning And Operational Readiness

#### Training And QC

- A training and QC plan is required before startup.
- QC must handle variable feedstock and recipe stability.
- Operator capability and turnover are risks that need training and procedures.

#### Reject And Sludge Handling

- Reject disposal and sludge handling must be resolved before startup.
- Rejects are part of the material balance at about 483.2 tonnes per year.

#### Marine Corrosion And Durability

- Salt and corrosion design basis must be included.
- Marine corrosion is a specific risk in the project risk register.

## S05 - Operations, Staffing, Financial Model, Scenarios, And Risks

This stage captures the operating model, economics, scenarios, and governance risks that Cognitive Memory must retain for later project discussions.

### Staffing And OPEX

#### Core Factory Staffing

- The core factory staffing model is 30 FTE.
- Loaded payroll is about 1,025,892 USD per year.
- The staffing model excludes a large self-owned collection fleet.
- The thermal process is a 24/7 operation.

#### Role Mix

- Roles include plant manager, process engineer, three shift supervisors, six kiln operators, five sorting/wash/mill operators, four packing/finishing operators, three forklift/loader roles, two maintenance mechanics, one electrical/automation role, one QC/lab role, one warehouse/logistics coordinator, and two procurement/admin/finance roles.

#### Base OPEX

- Base grid electricity cost is about 483,800 USD per year.
- Water cost is about 1,887 USD per year.
- Payroll is about 1,025,892 USD per year.
- Collection and transport is about 240,000 USD per year.
- Maintenance is about 290,000 USD per year.
- Admin and SG&A are about 200,000 USD per year.
- Packaging is about 195,000 USD per year.
- Additives are about 50,000 USD per year.
- Other variable costs are about 70,000 USD per year.
- Renewables O&M is about 70,000 USD per year.
- Ancillary direct costs are about 169,000 USD per year.

### Revenue And EBITDA

#### Official Base Integrated Case

- Official base integrated revenue is about 4,184,169 USD per year.
- OPEX is about 2,795,588 USD per year.
- EBITDA is about 1,388,582 USD per year.
- EBITDA margin is about 33.2 percent.

#### Scenario Envelope

- Conservative revenue is about 3,792,837 USD, OPEX about 3,229,711 USD, and EBITDA about 563,126 USD.
- Upside revenue is about 4,419,405 USD, OPEX about 2,513,322 USD, and EBITDA about 1,906,083 USD.

#### Ten-Year Lean Case

- Lean 70 integrated year 0 CAPEX is about -17.63 million USD.
- Revenue is about 2.51 million USD in year 1, 3.56 million USD in year 2, and 4.184 million USD from year 3 onward.
- OPEX is about 1.621 million USD in year 1, 2.297 million USD in year 2, and 2.702 million USD from year 3 onward.
- EBITDA from year 3 onward is about 1.482 million USD.
- Sustaining CAPEX from year 3 is about 440,750 USD.
- Cumulative cash flow remains about -7.148 million USD after year 10.

### Strategic Scenarios

#### Curacao Lean Base

- The 70-CW lean base case outputs 19,162 cubic meters per year.
- It needs 3,451 tonnes per year of glass and has about 3,427 tonnes secured.
- Coverage is about 0.993x.
- Revenue is about 4.184 million USD, OPEX about 2.702 million USD, EBITDA about 1.482 million USD, CAPEX about 17.63 million USD, and payback about 11.89 years.

#### ABC Growth Cases

- The 70-ABC secured case raises feedstock coverage to about 1.903x for the same line.
- The 90-ABC case outputs 24,638 cubic meters, needs 4,437 tonnes, has coverage about 1.48x, revenue about 5.276 million USD, EBITDA about 2.115 million USD, CAPEX about 18.83 million USD, and payback about 8.903 years.

#### Recommended 105-ABC Case

- The recommended 105-ABC case outputs 28,744 cubic meters.
- It needs 5,177 tonnes, has coverage about 1.269x, revenue about 6.096 million USD, OPEX about 3.551 million USD, EBITDA about 2.545 million USD, CAPEX about 19.88 million USD, and payback about 7.811 years.

#### Stretch 120-ABC Case

- The 120-ABC stretch case outputs 32,850 cubic meters.
- It needs 5,916 tonnes, has coverage about 1.11x, revenue about 6.915 million USD, EBITDA about 2.967 million USD, CAPEX about 20.82 million USD, and payback about 7.018 years.

### Ancillary Products

#### Micro-Fab Branch

- The formal micro-fab branch adds tourist, donor, and signature-piece products for visibility and branding.
- Artist or decor packs produce 3,000 units, 75,000 USD revenue, 42,000 USD EBITDA, and use about 300 kg glass.
- Keychains produce 12,000 units, 144,000 USD revenue, 72,000 USD EBITDA, and use about 420 kg glass.
- Donor or recognition plaques produce 140 units, 63,000 USD revenue, 37,000 USD EBITDA, and use about 280 kg glass.
- Epoxy tables or signature pieces produce 22 units, 79,200 USD revenue, 41,200 USD EBITDA, and use about 420 kg glass.
- Total ancillary revenue is about 361,200 USD with about 192,200 USD EBITDA and 1,420 kg glass use.
- Keychains should use bought-in preforms in phase 1, not custom injection tooling.

### Risk Register

#### High Impact Risks

- Feedstock shortfall is high probability and high impact; mitigations are contracts above base need, phased ramp, and Aruba/Bonaire top-up.
- Local selling price below plan is high probability and high impact; mitigations are pre-selling import-substitution value and diversifying aggregate and blocks.
- Brownfield contamination or hidden civil conditions are medium probability and high impact; mitigations are staged diligence before site commitment and contingency.
- Recipe instability is medium probability and high impact; mitigations are pilot trials, process-aid budget, and flexible QC.
- Grid quality, harmonics, and outages are medium probability and high impact; mitigations are power-factor correction, harmonic filters, BESS, and diesel-backed essential bus.

#### Operational And Timing Risks

- Renewable underperformance, operator capability or turnover, marine corrosion, permitting delay, and heat-recovery overbuild are also named risks.
- Heat-recovery overbuild is especially important because the corrected heat sink is small.

#### Payback Interpretation

- Base pure payback is about 12 to 15 years.
- Lean foam-only payback is about 13.7 years.
- Lean Curacao 70 payback is about 11.9 years.
- 105-ABC payback is about 7.8 years.

# AI Tap Intelligent Water Faucet - Time-Sliced Source Truth

This document is the normalized source of truth for loading the AI Tap project into CanDoItAll project structure and Cognitive Memory. It is derived from the business plan, budget workbooks, product-cost workbooks, water-measurement workbooks, XMind maps, GraphML/PPTX diagrams, and VisionSoftware slides in `codex/bundles/input/AI kohoutek`.

## S01 - Research, Problem, Market, And Boundaries

This stage captures why the product exists, which customer problems it solves, and which market assumptions must be remembered before any technical or financial reasoning.

### Problem And Value Proposition

#### Dishwashing Water Waste

- The business plan frames manual dishwashing as the primary water-waste problem.
- A measurement workbook records idle or empty water during dishwashing at roughly 2 minutes 45 seconds and about 9.167 liters.
- The same measurement set reports about 9.611 liters for the relevant measured washing total.
- The business plan states that up to 50 percent of dishwashing water may be wasted.
- The product value claim is not generic automation; it is water savings during ordinary sink use.

#### Scalding And Safety Use Case

- The faucet is also positioned as a safety feature for children and seniors.
- The core safety claim is that automatic temperature control can reduce accidental scalding from hot water.
- This safety use case supports buying the product for parents or grandparents, not only for the buyer's own household.

#### Household Savings Model

- The savings workbook assumes 10 liters per person per dishwashing event and 365 days per year.
- A three-person household saves about 10,950 liters per year.
- At 120 CZK per 1000 liters, the yearly water-price saving is about 1,314 CZK.
- With a planned faucet price of 4,600 CZK, the workbook payback is about 3.828 years.
- For 1,000,000 households, the workbook estimates 10,950,000 cubic meters of yearly water savings and about 1,201,653,000 CZK yearly price savings.

### Market Scope And Customer Segments

#### Primary B2C Market

- The primary buyers are households, especially buyers in roughly the 28 to 45 age range.
- Household demand is tied to water savings, convenience, ecology, and the safety story for children or seniors.
- The addressable installation base is described as hundreds of millions up to roughly one billion possible faucet installations.

#### Secondary B2B Market

- B2B targets include company kitchens, hotels, restaurants, hospitals, and schools.
- B2B value depends on water savings across repeated sink usage and on safety or hygiene positioning.
- B2B should not erase the B2C launch plan; it is a parallel channel.

#### Geographic Expansion Assumptions

- The source plan highlights Western and Central Europe.
- Additional named opportunities include the United States, especially California and New York.
- Other named expansion geographies are the UAE, Saudi Arabia, Israel, China, Australia, Korea, Japan, and Morocco.

### Competition And Intellectual Property

#### Substitute Products

- The known substitutes are manual lever faucets and infrared sensor faucets.
- The source plan says no direct same-product competitor was identified at the time.
- Infrared faucets are not equivalent because they do not recognize object type, object size, or required temperature/flow.

#### Patent And Utility Model Research

- Patent, utility-model, and standards research are mandatory early work items.
- The source plan treats availability and patent research as stage-one work before full development commitment.
- The memory must retain uncertainty here: the plan had not yet proven freedom to operate.

#### Standards And Certification Boundary

- Standards analysis belongs to the research and project-preparation stage.
- Certification is expected later during production preparation.
- The product combines water, power, electronics, and a camera, so certification is part of the business plan, not an optional afterthought.

### Sales, Channels, And Marketing

#### Physical Distributor Channels

- Planned physical distributor examples include Bauhaus, Ptacek, Gorenje, Sika, and IKEA.
- These channels matter because faucet installation and buyer trust favor established home-improvement and kitchen distribution.

#### Online And Direct Channels

- Planned online channels include Amazon, Mall, Alza, and CZC.
- A dedicated website is possible, but crowdfunding is recommended only when the product is already finished.

#### Marketing Work Packages

- Marketing is described as a key success factor.
- Planned marketing assets include corporate identity, a promotional video, and installation or instruction videos.
- Planned channels include trade fairs, social media, internet media, B2B conferences, designers, and kitchen designers.
- The source plan suggests considering a five-year or longer warranty as a trust signal.

## S02 - Product Concept, Technical Architecture, And Prototype Validation

This stage captures the product logic, hardware/software architecture, and prototype risks that must shape later development and testing.

### Functional Product Concept

#### Recognize Object And Set Water

- The faucet uses a camera and neural-network object recognition to identify what is under the spout.
- The system sets water temperature and flow intensity based on recognized object type and size.
- The product must distinguish object categories rather than simply detect motion.

#### Flow Regulation

- The plan distinguishes binary flow from multistage flow.
- Multistage flow is considered the better target because object size can affect needed water amount.
- One XMind concept proposes regulated valves near the faucet end and three baffles for off, one-third, two-thirds, and full flow.

#### Temperature Regulation

- Temperature regulation needs a feedback temperature sensor.
- Manual fallback mode requires four channels: two mechanical and two electronic.
- The control unit must receive feedback so the electronic path can regulate predictably.

### Software And Computer Vision

#### Recognition Categories

- VisionSoftware defines required recognition categories: small plate, large plate, bowl, glass, mug, spoon, other cutlery such as fork or knife, and human hands.
- The recognition output should include object identification, position, and relative or pixel size.
- The camera input can be grayscale.

#### Latency And Messaging

- VisionSoftware targets a 0.25 second check interval.
- A 0.5 second interval is the maximum fallback if 0.25 seconds is not realistic.
- The device should publish recognition data to an MQTT topic as JSON.

#### Runtime And License Constraints

- The software must run on Linux, ideally on ARM CPU.
- Final code should be C#/.NET Core unless Python has a large and measurable time advantage.
- GNU/GPL dependencies are disallowed; MIT, BSD, or otherwise free licenses are acceptable.
- TensorFlow is mentioned as a candidate neural-network technology.

### Hardware Architecture

#### Compute Unit

- Raspberry Pi-class hardware is considered enough for initial prototypes.
- Own hardware becomes worthwhile at expected volume.
- External electronics or hardware companies mentioned in the source are DFC Design and Hardwario.

#### Camera And Optics

- Camera reliability is a named risk because lens dirt, water drops, or limescale can corrupt image recognition.
- Mitigations mentioned include hydrophobic nano coating or automatic lens cleaning.
- A low-cost first-version concept used a PAL/NTSC camera and simple signal-change detection.

#### Power And Sleep Mode

- The source plan expects an external certified power adapter.
- A valve is estimated at about 0.25 A at 12 V.
- Low-power sleep and wake behavior is required; possible wake inputs include an IR sensor or accelerometer.
- Accelerometer gestures could support single, double, or triple tap commands.
- No mobile application is planned initially.

### Prototype Paths And Technical Risk

#### First Simple Detection Version

- The first XMind concept uses camera image-change detection to start water.
- The rough circuit includes rectification/filtering, average signal, LM358 comparator, 555 timer or cheap 8-bit MCU with ADC, and transistor control for the solenoid.
- Estimated simple-version power includes camera about 50 mA at 12 V, valve 10 to 20 mA at 12 V, and other electronics about 2 mA.

#### Advanced Recognition Version

- The advanced XMind concept uses a USB waterproof camera, Raspberry Pi, and a module for valves and temperature measurement.
- The advanced version should regulate by object size and neural-network classification.
- Servo actuation is considered but humidity and failure risk make it unattractive.

#### Validation Demo Boundary

- Principle validation requires a kitchen-object recognition library.
- It also requires a pre-prototype sink setup.
- Validation must prove the core function before detailed prototype design and production preparation.

## S03 - Development, Production Preparation, And Launch Sequence

This stage captures how the idea turns into a manufacturable product and when production launch is expected.

### Development Work Packages

#### Research And Project Preparation

- The first project stage is availability research, patent research, standards analysis, project plan, and budget.
- This stage gates whether the project should continue into technical validation.

#### Principle Validation

- The second project stage is object-recognition validation, pre-prototype sink setup, and basic function validation.
- It should prove object recognition and automatic water behavior before spending on final mechanics.

#### Prototype Design

- Prototype design includes detailed prototype design, industrial product design, expansion of the pattern library, prototype manufacturing, and testing.
- The source plan expects electronics, software, and mechanics to be outsourced initially.

### Development Timing

#### External Development Duration

- Electronics development is estimated at about 9 months.
- Software development is estimated at about 9 to 13 months.
- Mechanics development is estimated at about 11 months.
- Production preparation is estimated at about 6 months.

#### First Test Series Milestone

- The plan expects roughly 18 months from project start to the first test series.
- The first test series belongs after prototype design and production preparation, not before technical validation.

#### Internal Development Shift

- Internal mechanical or broader development capacity may be created later.
- The source plan points to an internal development department around year 4.

### Production Preparation

#### Certification And Production Procedure

- Production preparation includes certification, production procedures, test procedures, production technology, and first test series.
- Certification is budgeted in 2020.

#### Tooling And Equipment

- Seed-stage production preparation includes about 5.0 million CZK in 2021.
- Named 2021 production-preparation items include plastic and metal molds at about 3.2 million CZK.
- Other named items include test fixture development at about 0.8 million CZK and assembly or storage equipment at about 1.0 million CZK.

#### Manufacturing Process

- The planned manufacturing chain includes input-material processing, plastic part pressing, metal parts, semi-finished machining, valve assembly and testing, control-unit assembly and testing, faucet assembly and testing, system test, packaging, and dispatch.
- Plastic and metal part production is likely outsourced.
- Internal work centers focus on assembly, testing, storage, and dispatch.

### Launch And Volume Ramp

#### First Sales Year

- The budget workbook shows no revenue in 2020 and first revenue in 2021.
- Unit sales in 2021 are planned at 950 AI faucets.
- 2021 revenue is planned at 4,370,000 CZK.

#### Break-Even Ramp

- The business plan says break-even should occur in year 3, calendar year 2022, at 17,500 units.
- Budgeted 2022 unit sales are 17,500 and planned revenue is 80,500,000 CZK.

#### Return Of Investment

- The business plan says the initial investment should be fully returned in year 5, calendar year 2024.
- The budget workbook running cash flow turns positive in 2024.

## S04 - Organization, Operations, Team Growth, And Scale-Up

This stage captures team structure, operational scale, production capacity, and construction or facility assumptions.

### Organization Roadmap

#### 2020 Team

- The 2020 organization map has the CEO with a production manager, technologist, and sales role.
- Early development is mostly outsourced.
- Internal staff supports project management, production preparation, and initial sales.

#### 2021 To 2023 Team Growth

- The 2021 organization map adds production workers, warehouse, purchasing, quality, sales, technologist, and office manager roles.
- The 2022 map is similar but grows production workers.
- The 2023 map adds CCO, production manager, nine production workers, two warehouse roles, two purchasing roles, quality, safety, technologist, sales with support, and office manager.

#### 2024 To 2025 Organization

- The 2024 map adds a CCO, development manager, two mechanical designers, CFO, HR, expanded production, warehouse, purchasing, quality, safety, sales, and technical support.
- The 2025 map keeps a similar structure with about 17 production workers.
- This stage is where the company moves from outsourced development toward an internal product and operations organization.

### Payroll And Staffing Budget

#### Wage Categories

- Budgeted wage lines include CEO, production manager, technologist, salespeople, production workers, warehouse and purchasing, office or accounting, quality, production director, HR, safety, support, development manager, two mechanical designers, CCO, CFO, and bonuses.

#### Production Worker Ramp

- Production worker wages are budgeted at 0 CZK in 2020.
- Production worker wages rise to 1,365,000 CZK in 2021, 2,940,000 CZK in 2022, 3,780,000 CZK in 2023, 5,740,000 CZK in 2024, and 7,140,000 CZK in 2025.

#### Total Wage Ramp

- Total wages are budgeted at 1,720,000 CZK in 2020.
- They increase to 4,640,000 CZK in 2021, 11,230,000 CZK in 2022, 15,860,000 CZK in 2023, 23,330,000 CZK in 2024, and 27,140,000 CZK in 2025.

### Facilities And Construction Assumptions

#### Rent Or Mortgage Path

- Rent or mortgage is budgeted at 490,000 CZK in 2020.
- It grows to 1,800,000 CZK in 2021 and 2022, 3,000,000 CZK in 2023 and 2024, and 4,200,000 CZK in 2025.

#### Production Hall Decision

- The business plan recommends considering investment in an owned production hall in year 3 or year 4.
- That decision should wait until sales plans are more certain.
- Rental cost is estimated at about 14.3 million CZK across six years.

#### Equipment Acquisition Ramp

- Equipment acquisition is budgeted at 260,000 CZK in 2020.
- It rises to 1,230,000 CZK in 2021, 3,070,000 CZK in 2022, 10,570,000 CZK in 2023, 11,080,000 CZK in 2024, and 18,560,000 CZK in 2025.

### Operating Controls

#### Quality And Test Workplaces

- The production process requires valve assembly testing, control-unit assembly testing, faucet assembly testing, and final system testing.
- Quality roles appear in the organization from the early production years.

#### Remote Updates And Connectivity

- Connectivity options include ethernet, bluetooth, and wifi.
- Remote software updates are a planned capability.
- Future product data could include water-consumption collection.

#### Future Product Extensions

- Future features include instant water heating, water consumption monitoring, and design series.
- These extensions must not be confused with the first launch scope.

## S05 - Finance, Unit Economics, Funding, And Long-Term Viability

This stage captures the financial model, planned investments, cash-flow path, and where the product economics are fragile.

### Product Unit Economics

#### Current Product Cost Estimate

- The v2 product-cost workbook estimates total product cost at 3,110 CZK.
- The planned distributor or customer price is 4,600 CZK.
- Gross margin is 1,490 CZK per unit.

#### Cost Breakdown

- The v2 cost components are camera 120 CZK, mainboard 850 CZK, connectors 100 CZK, adapter 170 CZK, mainboard box 60 CZK, faucet 1,760 CZK, packaging 40 CZK, and manual 10 CZK.

#### High-Volume Scenario

- At 50,000 units per year, the workbook model implies 230,000,000 CZK revenue.
- Total costs in that scenario are 155,500,000 CZK.
- Gross margin is 74,500,000 CZK.

### Funding And Investment Plan

#### Pre-Seed Funding

- Pre-seed funding is estimated at about 10.5 million CZK.
- Pre-seed covers external development, prototype work, and approval preparation in the first year.

#### Seed Funding

- Seed funding is estimated at about 18.3 million CZK excluding material.
- Seed covers production preparation, first series, and sales start in the second year.

#### Total Initial Funding

- Total start through seed completion is about 28.8 million CZK.
- A working-capital or revolving loan is also needed for material purchases once production volume ramps.

### Six-Year Budget

#### Revenue And Unit Sales

- Planned unit sales are 0 in 2020, 950 in 2021, 17,500 in 2022, 32,200 in 2023, 54,000 in 2024, and 78,000 in 2025.
- Planned revenue is 0 CZK in 2020, 4,370,000 CZK in 2021, 80,500,000 CZK in 2022, 148,120,000 CZK in 2023, 248,400,000 CZK in 2024, and 358,800,000 CZK in 2025.
- Total planned sales across 2020 to 2025 are 182,650 devices.

#### Expense Growth

- Total expenses are budgeted at 10,570,280 CZK in 2020.
- They rise to 22,629,780 CZK in 2021, 76,729,280 CZK in 2022, 136,214,680 CZK in 2023, 217,160,480 CZK in 2024, and 309,168,480 CZK in 2025.
- Material purchase is the largest ramping cost: 6,700,000 CZK in 2020, 10,194,500 CZK in 2021, 55,625,000 CZK in 2022, 101,342,000 CZK in 2023, 172,140,000 CZK in 2024, and 249,780,000 CZK in 2025.

#### Cash Flow

- Annual cash-flow change is -10,570,280 CZK in 2020 and -18,258,830 CZK in 2021.
- It turns positive at 3,788,220 CZK in 2022, then 11,937,520 CZK in 2023, 31,293,520 CZK in 2024, and 49,709,520 CZK in 2025.
- Running cash flow is -10,570,280 CZK in 2020, -28,829,110 CZK in 2021, -25,040,890 CZK in 2022, -13,103,370 CZK in 2023, +18,190,150 CZK in 2024, and +67,899,670 CZK in 2025.

### Investment Category Detail

#### Development And Development Material

- Development and development material are budgeted at 6,700,000 CZK in 2020 and 7,240,000 CZK in 2021.
- After launch, this line is 1,200,000 CZK in 2022 and 2023, then rises to 4,200,000 CZK in 2024 and 7,200,000 CZK in 2025.

#### Marketing Budget

- Marketing is budgeted at 468,000 CZK in 2020, 3,770,000 CZK in 2021, 3,830,000 CZK in 2022, 3,950,000 CZK in 2023, 5,330,000 CZK in 2024, and 6,170,000 CZK in 2025.

#### Production Material

- Manufacturing material is 0 CZK in 2020.
- It grows to 2,954,500 CZK in 2021, 54,425,000 CZK in 2022, 100,142,000 CZK in 2023, 167,940,000 CZK in 2024, and 242,580,000 CZK in 2025.

### Financial Risks And Memory Checks

#### Working-Capital Exposure

- The model requires material purchasing before product cash is collected.
- The business plan explicitly expects a working-capital loan for production-material purchases.

#### Gross Margin Sensitivity

- The current v2 gross margin is 1,490 CZK per unit at a 4,600 CZK price.
- The older cost estimate had lower cost and different distributor/customer price assumptions, so memory should prefer the v2 estimate unless asked about historical assumptions.

#### Break-Even Consistency

- The memory must retain that break-even is linked to 2022 and 17,500 units.
- The 2024 positive running cash flow supports the year-5 return claim.

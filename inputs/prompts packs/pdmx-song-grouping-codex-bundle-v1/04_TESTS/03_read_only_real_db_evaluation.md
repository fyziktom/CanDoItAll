# Copied Real-DB Evaluation Procedure

## Objective

Validate the grouping system against realistic data without touching the original DB.

## Procedure

### Step 1
Determine the active provider and connection string.

### Step 2
Create a safe copy/snapshot.

### Step 3
Run migrations on the copy only.

### Step 4
Run:
- profile refresh
- missing embedding generation
- dry-run grouping

### Step 5
Export metrics:
- group count
- suspicious clusters
- review volume
- top large blocks
- auto-accept count

### Step 6
Sample audit:
- 50 high-confidence clusters
- 50 review-band clusters
- 25 suspicious large clusters

## What to log

- block-size distribution
- top hot blocks
- candidate pair counts
- cluster sizes
- average confidence per cluster
- exact vs review vs reject counts
- timings per stage

## What not to do

- do not point migration/apply flows at the original DB
- do not trust only aggregate metrics
- do not skip manual sampling

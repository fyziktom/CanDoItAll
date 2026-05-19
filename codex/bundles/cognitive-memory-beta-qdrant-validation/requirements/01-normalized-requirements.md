# Normalized Requirements

| Id | Requirement | Validation |
| --- | --- | --- |
| CM-BETA-001 | Audit P0 and P1 beta prerequisites and identify any blocker that must be fixed before beta. | Source/docs review plus execution report gate row. |
| CM-BETA-002 | Validate Docker infrastructure for Qdrant and PostgreSQL and capture exact health/connectivity proof. | `docker ps`, Qdrant REST/gRPC/app status, PostgreSQL profile/status proof. |
| CM-BETA-003 | Execute live Qdrant projection rebuild through the app/API with durable Cognitive Memory inputs. | Rebuild API result with projected items or fixed failure followed by projected items; Qdrant collection/points proof. |
| CM-BETA-004 | Validate recall/vector behavior through the app/API and prove vector projection is used or fix the blocker. | Recall trace/stage proof showing vector projection use, not only skipped/unavailable warnings. |
| CM-BETA-005 | Validate operator visibility for projection status/audit in browser after live Qdrant validation. | Playwright route, viewport, assertions, screenshots, and console log. |
| CM-BETA-006 | Update docs/roadmap/stage to beta only if all beta gates pass; otherwise record the true blocker. | Docs diff plus completed-stage bundle validator. |


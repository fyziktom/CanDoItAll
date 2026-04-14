# Framework sources

## NIST SP 800-218 / Secure Software Development Framework (SSDF) v1.1

- Key: `nist-ssdf`
- URL: https://csrc.nist.gov/pubs/sp/800/218/final
- Focus: Lifecycle-wide secure software development baseline
- Why used: Primary backbone for preparation, protection, implementation hygiene, verification, release evidence, and vulnerability response.
- License note: Public U.S. government publication; referenced and transformed into original template content rather than copied verbatim.
- Extracted patterns:
- Prepare the organization with explicit roles, policies, and supply-chain expectations.
- Protect software assets, environments, and sensitive development artifacts.
- Produce well-secured software through planned implementation and verification activities.
- Respond to vulnerabilities with explicit triage, remediation, and learning loops.

## NIST SP 800-218A / Secure Software Development Practices for AI Systems and Components

- Key: `nist-ssdf-ai`
- URL: https://csrc.nist.gov/pubs/sp/800/218/a/final
- Focus: AI-specific secure development, evaluation, and model governance additions
- Why used: Adds evaluation, model provenance, prompt governance, safety review, and evidence expectations for AI-assisted change delivery.
- License note: Public U.S. government publication; referenced and transformed into original template content rather than copied verbatim.
- Extracted patterns:
- Track model and component provenance before use in delivery flows.
- Define AI-specific evaluation gates and refusal conditions.
- Separate experimentation from approved production usage.
- Retain traceable evidence for model and prompt decisions.

## OWASP SAMM

- Key: `owasp-samm`
- URL: https://owasp.org/www-project-samm/
- Focus: Risk-driven maturity model for software assurance
- Why used: Used to design maturity-friendly controls, explicit checkpoints, and measurable governance criteria across templates.
- License note: Open community framework; bundle uses original derived content and mapping notes.
- Extracted patterns:
- Define maturity progression instead of one-size-fits-all process rigidity.
- Keep controls measurable and tailored to organizational risk.
- Cover the full software lifecycle including development and acquisition.
- Use governance, design, implementation, verification, and operations as improvement lenses.

## OpenChain reference resources

- Key: `openchain`
- URL: https://openchainproject.org/resources
- Focus: Open-source compliance, policy templates, checklists, and flow guidance
- Why used: Used for open-source intake, review accountability, reusable checklist structure, and license/supply-chain governance.
- License note: OpenChain reference material includes CC-0 reference assets; bundle still uses newly authored derivative process content.
- Extracted patterns:
- Maintain reusable policy and checklist structures for open-source intake.
- Track license obligations and escalation points explicitly.
- Separate shared reference material from product-specific decisions.
- Treat supply-chain governance as an auditable operational discipline.

## SPDX

- Key: `spdx`
- URL: https://spdx.dev/about/overview/
- Focus: Standardized SBOM and software metadata exchange
- Why used: Provides vocabulary and expectations for SBOM, component provenance, dependency evidence, and later reuse decisions.
- License note: Open standard; bundle uses terminology and compatibility guidance, not copied specification prose.
- Extracted patterns:
- Store bill-of-materials information in structured reusable form.
- Track provenance, license, security, and related metadata together.
- Prefer standardized metadata over ad hoc component notes.
- Enable later sharing of package and dependency evidence.

## SLSA / Supply-chain Levels for Software Artifacts

- Key: `slsa`
- URL: https://slsa.dev/
- Focus: Provenance, build integrity, and supply-chain assurance
- Why used: Used for artifact provenance expectations, build evidence, tamper-resistance thinking, and release integrity guidance.
- License note: Open community specification; bundle uses original summaries and control mappings.
- Extracted patterns:
- Treat provenance as a first-class artifact.
- Differentiate assurance depth by risk and maturity.
- Carry forward build and dependency trust information into release decisions.
- Preserve enough evidence for later audit and forensic replay.

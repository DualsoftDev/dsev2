# Ev2.Cpu Documentation Index

Use this entrypoint to navigate the Ev2.Cpu knowledge base. Content is organised by audience and intent, with maintenance metadata maintained in `docs/.meta/doc-registry.yaml`.

---

## 📌 Quick Links by Audience

| Audience | Start Here | Deep Dives |
|----------|------------|------------|
| **New developer** | `reference/Ev2.Cpu-API-Reference.md#quick-reference` | `guides/quickstarts/PLC-Code-Generation-Guide.md`, `guides/integration/3rdParty-Integration-Guide.md` |
| **PLC programmer** | `guides/manuals/generation/Ev2.Cpu.Generation-사용자매뉴얼.md#빠른-시작` | `guides/manuals/standard-library/Ev2.Cpu.StandardLibrary-사용자매뉴얼.md`, `reference/Ev2.Cpu.StandardLibrary-Reference.md#빠른-인덱스` |
| **Architect / Maintainer** | `concepts/ARCHITECTURE.md` | `specs/runtime/RuntimeSpec.md`, module specs in `specs/` folders, `concepts/platform-roadmap.md` |

---

## 📁 Directory Overview

- `concepts/`
  - `ARCHITECTURE.md` – High-level architecture & dependency map
  - `platform-roadmap.md` – Product-wide roadmap (draft)
- `guides/`
  - `quickstarts/` – Hands-on introductions and walk-throughs
  - `operations/` – Operational runbooks such as retain memory handling
  - `integration/` – External system integration scenarios
  - `migration/` – Upgrade/migration checklists
  - `manuals/` – Product manuals (multi-language)
- `reference/`
  - `Ev2.Cpu-API-Reference.md` – API quick reference (auto-generated)
  - `Ev2.Cpu.StandardLibrary-Reference.md` – IEC 61131-3 block catalogue
  - `glossary.md` – Shared terminology (draft)
- `specs/`
  - `core/` – Core language & type system specs
  - `runtime/` – Runtime engine specs and deep dives
  - `codegen/` – Generation pipeline specs
  - `protocols/` – Protocol adapter specs
- `playbooks/`
  - Release, incident, and environment setup runbooks

> Historical reports previously in `docs/reports/` were merged into module maintenance sections. New documents must be registered in `docs/.meta/doc-registry.yaml`.

---

## 🔍 Navigation Aids

- **Module Specs**: Core (`specs/core/Ev2.Cpu.Core.md`), Runtime (`specs/runtime/Ev2.Cpu.Runtime.md`), CodeGen (`specs/codegen/Ev2.Cpu.CodeGen.md`), Protocols (`specs/protocols/Ev2.Protocols.DevelopmentSpec.md`).
- **Maintenance & QA**: Each spec contains an `AS-IS / TO-BE / Backlog` roadmap; see the dedicated sections for test strategies and key metrics.
- **Observability**: Runtime telemetry and diagnostics are detailed in `specs/runtime/RuntimeSpec.md` with links back to operational guides.

---

## 📅 Update Log

- 2025-10-26 – Root README introduced; documentation structure aligned with audience-centric layout.
- 2025-10-26 – Guides reorganised by purpose; manual and quickstart content separated.

Refer to Git history and `docs/.meta/doc-registry.yaml` for earlier revisions.

---

**Maintainer**: Runtime/Docs Team (reach via repository issues)  
**Last Reviewed**: 2025-10-26

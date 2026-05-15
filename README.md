# Agentic System Template

A drop-in, project-agnostic multi-agent orchestration system for Claude Code. Unpack into the root of any repository and call the Main Orchestrator — it will inspect the project on first run, fill in the project context automatically, and route every task to the right specialist agents.

## What's inside

```
.claude/
├── agents/                          54 agent definitions
│   └── __💥⋆༺𓆩☠︎︎𓆪༻⋆💥__.agent.md   Main Orchestrator (entry point)
├── ORCHESTRATION_GUIDE.md           Dispatch rules, parallel/sequential patterns, agent directory
├── PROJECT_CONTEXT.md               Template — auto-filled by the Main Orchestrator on first run
└── settings.json                    Permissions for common tools (git, npm, docker, etc.)
output/                              Agent results land here (gitignored)
├── engineering/
├── security/
├── data/
├── platform/
├── business/
└── bim/
```

## Install

1. Unzip into the root of your project so that `.claude/` and `output/` sit next to your source code.
2. Open the project in Claude Code.
3. Address the Main Orchestrator: `@__💥⋆༺𓆩☠︎︎𓆪༻⋆💥__ <your task>`

On the very first run the orchestrator detects that `PROJECT_CONTEXT.md` is still in template state, inspects your repository (package manifests, Dockerfiles, source layout, migrations, OpenAPI, docs), and writes a populated `PROJECT_CONTEXT.md` before processing your task. After that, it is read once per session and never re-read mid-session.

## Domains covered

Engineering · Security · Data & AI · Platform/Ops · Business/PM · BIM/Construction

## Customising

- **Add a new specialist:** drop a new `*.agent.md` file into `.claude/agents/`. The Main Orchestrator picks it up automatically — no registration step.
- **Tighten permissions:** edit `.claude/settings.json` (`allow` / `deny` lists).
- **Refresh project context:** delete `.claude/PROJECT_CONTEXT.md` (or restore it from this template) — the orchestrator will rebuild it on the next session.

## Notes

- The `output/` directory is for agent artifacts only. Add it to `.gitignore` if you don't want to track results.
- Agent files are Markdown with YAML frontmatter; edit them like any other doc.
- The orchestrator never executes implementation work itself — it only plans, dispatches, and synthesises.

# CLAUDE.md — BombasticIFC

## Orchestrator session protocol

Before processing any task, read:
1. `.claude/ORCHESTRATION_GUIDE.md`
2. `.claude/PROJECT_CONTEXT.md`

After each agent completes:
- Write output to `output/{agent_name}_{timestamp}.md`
- Append a row to `output/progress.md`

After all agents complete:
- Write final synthesis to `output/final_summary.md`

---

## First-run checklist

See `.claude/FIRST_RUN_CHECKLIST.md` for the full session bootstrap procedure.

import Step from "@mui/material/Step";
import StepLabel from "@mui/material/StepLabel";
import Stepper from "@mui/material/Stepper";
import { testIds } from "@/constants/testIds";
import {
  claimProgressSteps,
  type ClaimProgressStep,
} from "@/realtime/contracts";
import { claimProgressStrings } from "@/strings/claims";

const finalIndex = 3;

function indexOf(step: ClaimProgressStep): number {
  if (step === claimProgressSteps.retrieving) return 0;
  if (step === claimProgressSteps.reasoning) return 1;
  if (step === claimProgressSteps.reviewing) return 2;
  return finalIndex;
}

type ItemState = "done" | "current" | "pending" | "failed";

function itemStates(seen: readonly ClaimProgressStep[]): ItemState[] {
  const latest = seen[seen.length - 1];
  if (latest === claimProgressSteps.completed) {
    return ["done", "done", "done", "done"];
  }
  if (latest === claimProgressSteps.failed) {
    const working = seen.filter(
      (s) =>
        s === claimProgressSteps.retrieving ||
        s === claimProgressSteps.reasoning ||
        s === claimProgressSteps.reviewing,
    );
    const failedAt = working.length ? indexOf(working[working.length - 1]) : 0;
    return [0, 1, 2, finalIndex].map((i): ItemState => {
      if (i === finalIndex) return "failed";
      return i < failedAt ? "done" : "failed";
    });
  }
  const active = indexOf(latest);
  return [0, 1, 2, finalIndex].map((i): ItemState =>
    i < active ? "done" : i === active ? "current" : "pending",
  );
}

const stateText: Record<ItemState, string> = {
  done: claimProgressStrings.stateDone,
  current: claimProgressStrings.stateCurrent,
  pending: claimProgressStrings.statePending,
  failed: claimProgressStrings.stateFailed,
};

/** Vertical step list; each step's state is spelled out as text, not colour alone. */
export function ClaimProgressSteps({
  seen,
}: {
  seen: readonly ClaimProgressStep[];
}) {
  if (seen.length === 0) return null;
  const states = itemStates(seen);
  const failed = seen[seen.length - 1] === claimProgressSteps.failed;
  const labels = [
    claimProgressStrings.retrieving,
    claimProgressStrings.reasoning,
    claimProgressStrings.reviewing,
    failed ? claimProgressStrings.failed : claimProgressStrings.done,
  ];
  const activeStep = states.findIndex((s) => s === "current" || s === "failed");
  return (
    <Stepper
      orientation="vertical"
      activeStep={activeStep === -1 ? labels.length : activeStep}
      aria-label={claimProgressStrings.listLabel}
    >
      {labels.map((label, i) => (
        <Step
          key={label}
          completed={states[i] === "done"}
          data-testid={testIds.claimProgressStep}
          data-state={states[i]}
        >
          <StepLabel
            error={states[i] === "failed"}
            optional={stateText[states[i]]}
          >
            {label}
          </StepLabel>
        </Step>
      ))}
    </Stepper>
  );
}

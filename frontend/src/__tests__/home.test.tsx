import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import Home from "@/app/page";
import { appStrings } from "@/strings/app";

describe("Home page", () => {
  it("renders the main landmark with a single h1", () => {
    render(<Home />);
    expect(screen.getByRole("main")).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 1, name: appStrings.welcome })).toBeInTheDocument();
    expect(screen.getByText(appStrings.intro)).toBeInTheDocument();
  });
});

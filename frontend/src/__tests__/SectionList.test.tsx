import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { SectionList } from "@/components/molecules/SectionList";

describe("SectionList", () => {
  it("renders headings in order with chunk counts", () => {
    render(
      <SectionList
        labelledBy="h"
        sections={[
          { order: 0, heading: "PREAMBLE", chunkCount: 1 },
          { order: 1, heading: "WHAT IS NOT COVERED", chunkCount: 3 },
        ]}
      />,
    );
    const items = screen.getAllByRole("listitem");
    expect(items).toHaveLength(2);
    expect(items[0]).toHaveTextContent("PREAMBLE1 chunk");
    expect(items[1]).toHaveTextContent("3 chunks");
  });
});

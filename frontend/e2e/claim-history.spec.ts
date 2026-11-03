import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { routes } from "../src/constants/routes";
import { testIds } from "../src/constants/testIds";
import { claimHistoryStrings } from "../src/strings/claims";

const API_URL = process.env.E2E_API_URL ?? "http://localhost:5080";
const LINES = ["SCHEDULE OF COVERAGE", "The engine is covered for five years."];
const PAGE_SIZE = 20;

function buildPdf(lines: string[]): Buffer {
  const content = `BT /F1 12 Tf 50 750 Td 20 TL ${lines.map((l) => `(${l}) Tj T*`).join(" ")} ET`;
  const objects = [
    "<< /Type /Catalog /Pages 2 0 R >>",
    "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
    "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
    `<< /Length ${content.length} >>\nstream\n${content}\nendstream`,
    "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
  ];
  let out = "%PDF-1.4\n";
  const offsets: number[] = [];
  objects.forEach((body, i) => {
    offsets.push(out.length);
    out += `${i + 1} 0 obj\n${body}\nendobj\n`;
  });
  const xref = out.length;
  out += `xref\n0 ${objects.length + 1}\n0000000000 65535 f \n`;
  for (const o of offsets) out += `${String(o).padStart(10, "0")} 00000 n \n`;
  out += `trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`;
  return Buffer.from(out, "latin1");
}

async function uploadAndOpen(page: Page, title: string): Promise<string> {
  await page.goto(routes.contracts);
  await page.getByTestId(testIds.uploadTitleInput).fill(`${title} ${Date.now()}`);
  await page.getByTestId(testIds.uploadFileInput).setInputFiles({
    name: "warranty.pdf",
    mimeType: "application/pdf",
    buffer: buildPdf(LINES),
  });
  await page.getByTestId(testIds.uploadSubmit).click();
  await expect(page).toHaveURL(/\/contracts\/[0-9a-f-]{36}$/);
  return /contracts\/([0-9a-f-]{36})$/.exec(page.url())![1];
}

async function expectNoSeriousViolations(page: Page) {
  const results = await new AxeBuilder({ page })
    .include(`[data-testid="${testIds.claimHistory}"]`)
    .withTags(["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"])
    .analyze();
  const serious = results.violations.filter((v) => v.impact === "serious" || v.impact === "critical");
  expect(serious.map((v) => `${v.id}: ${v.help}`)).toEqual([]);
}

test("a contract with no claims shows the empty state", async ({ page }) => {
  await uploadAndOpen(page, "History empty");

  const section = page.getByTestId(testIds.claimHistory);
  await expect(section).toContainText(claimHistoryStrings.heading);
  await expect(page.getByTestId(testIds.claimHistoryEmpty)).toContainText(claimHistoryStrings.empty);
  await expectNoSeriousViolations(page);
});

test("a newly evaluated claim is at the top of the history and its row opens the result", async ({ page }) => {
  const contractId = await uploadAndOpen(page, "History flow");
  for (const text of ["Is the engine covered? first", "Is the engine covered? second"]) {
    await page.getByTestId(testIds.claimTextInput).fill(text);
    await page.getByTestId(testIds.claimSubmit).click();
    await expect(page).toHaveURL(/\/contracts\/[0-9a-f-]{36}\/claims\/[0-9a-f-]{36}$/);
    await page.goto(routes.contractDetail(contractId));
  }

  const rows = page.getByTestId(testIds.claimHistoryRow);
  await expect(rows).toHaveCount(2);
  await expect(rows.first()).toContainText("second");
  await expect(rows.last()).toContainText("first");
  await expectNoSeriousViolations(page);

  await rows.first().getByRole("link").click();
  await expect(page).toHaveURL(/\/contracts\/[0-9a-f-]{36}\/claims\/[0-9a-f-]{36}$/);
  await expect(page.getByTestId(testIds.claimText)).toContainText("second");
});

test("Load more appends the next page and moves focus to the first new row", async ({ page }) => {
  test.setTimeout(120_000);
  const contractId = await uploadAndOpen(page, "History paging");
  const total = PAGE_SIZE + 3;
  for (let i = 1; i <= total; i++) {
    const response = await page.request.post(`${API_URL}/api/contracts/${contractId}/claims`, {
      data: { claimText: `Is the engine covered? seeded ${i}` },
    });
    expect(response.status()).toBe(201);
  }
  await page.goto(routes.contractDetail(contractId));

  const rows = page.getByTestId(testIds.claimHistoryRow);
  await expect(rows).toHaveCount(PAGE_SIZE);
  await expect(rows.first()).toContainText(`seeded ${total}`);

  await page.getByTestId(testIds.claimHistoryLoadMore).click();

  await expect(rows).toHaveCount(total);
  await expect(rows.nth(PAGE_SIZE).getByRole("link")).toBeFocused();
  await expect(page.getByTestId(testIds.claimHistoryAnnounce)).toContainText(claimHistoryStrings.moreLoaded(3));
  await expect(page.getByTestId(testIds.claimHistoryLoadMore)).toHaveCount(0);
  await expectNoSeriousViolations(page);
});

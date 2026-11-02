import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import { routes } from "../src/constants/routes";
import { testIds } from "../src/constants/testIds";
import { indexStrings } from "../src/strings/contracts";

const LINES = [
  "SCHEDULE OF COVERAGE",
  "The engine is covered for five years.",
  "WHAT IS NOT COVERED",
  "Wear and tear is excluded.",
];

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

test("uploading a PDF shows the Indexed chip and the sections list", async ({ page }) => {
  await page.goto(routes.contracts);
  await page.getByTestId(testIds.uploadTitleInput).fill(`Indexed contract ${Date.now()}`);
  await page.getByTestId(testIds.uploadFileInput).setInputFiles({
    name: "warranty.pdf",
    mimeType: "application/pdf",
    buffer: buildPdf(LINES),
  });
  await page.getByTestId(testIds.uploadSubmit).click();

  await expect(page).toHaveURL(/\/contracts\/[0-9a-f-]{36}$/);
  await expect(page.getByTestId(testIds.contractDetailTitle)).toBeVisible();
  await expect(page.getByTestId(testIds.indexStatusChip).locator('visible=true')).toHaveText(indexStrings.status.Indexed);
  await expect(page.getByTestId(testIds.indexSections)).toBeVisible();
  const items = page.getByTestId(testIds.indexSectionItem);
  await expect(items).toHaveCount(2);
  await expect(items.nth(0)).toContainText(LINES[0]);
  await expect(items.nth(1)).toContainText(LINES[2]);
  await expect(page.getByTestId(testIds.indexRetry)).toHaveCount(0);

  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"]).analyze();
  const serious = results.violations.filter((v) => v.impact === "serious" || v.impact === "critical");
  expect(serious.map((v) => `${v.id}: ${v.help}`)).toEqual([]);
});

import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { routes } from "../src/constants/routes";
import { testIds } from "../src/constants/testIds";

const BODY_TEXT = "The supplier shall deliver the goods within thirty days.";

function buildPdf(text: string | null): Buffer {
  const content = text ? `BT /F1 12 Tf 50 700 Td (${text}) Tj ET` : "";
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

async function expectNoSeriousViolations(page: Page) {
  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"]).analyze();
  const serious = results.violations.filter((v) => v.impact === "serious" || v.impact === "critical");
  expect(serious.map((v) => `${v.id}: ${v.help}`)).toEqual([]);
}

test("uploads a PDF, lands on the detail page and sees it in the list", async ({ page }) => {
  const title = `E2E contract ${Date.now()}`;
  await page.goto(routes.contracts);
  await expectNoSeriousViolations(page);

  await page.getByTestId(testIds.uploadTitleInput).fill(title);
  await page.getByTestId(testIds.uploadFileInput).setInputFiles({
    name: "supply.pdf",
    mimeType: "application/pdf",
    buffer: buildPdf(BODY_TEXT),
  });
  await page.getByTestId(testIds.uploadSubmit).click();

  await expect(page).toHaveURL(/\/contracts\/[0-9a-f-]{36}$/);
  await expect(page.getByTestId(testIds.contractDetailTitle)).toHaveText(title);
  await expect(page.getByTestId(testIds.contractDetailFileName)).toContainText("supply.pdf");
  await expect(page.getByTestId(testIds.contractDetailPages)).toContainText("1");
  await expect(page.getByTestId(testIds.contractDetailText)).toContainText(BODY_TEXT);
  await expectNoSeriousViolations(page);

  await page.getByTestId(testIds.contractDetailBack).click();
  await expect(page).toHaveURL(routes.contracts);
  const link = page.getByTestId(testIds.contractItemLink).filter({ hasText: title });
  await expect(link).toBeVisible();
  await expectNoSeriousViolations(page);
});

test("blocks a blank title client-side", async ({ page }) => {
  await page.goto(routes.contracts);
  await page.getByTestId(testIds.uploadFileInput).setInputFiles({
    name: "a.pdf",
    mimeType: "application/pdf",
    buffer: buildPdf(BODY_TEXT),
  });
  await page.getByTestId(testIds.uploadSubmit).click();

  await expect(page.getByTestId(testIds.uploadTitleError)).toBeVisible();
  await expect(page).toHaveURL(routes.contracts);
});

test("shows the file error next to the file field for a non-PDF file", async ({ page }) => {
  await page.goto(routes.contracts);
  await page.getByTestId(testIds.uploadTitleInput).fill("Not a pdf");
  await page.getByTestId(testIds.uploadFileInput).setInputFiles({
    name: "notes.pdf",
    mimeType: "application/pdf",
    buffer: Buffer.from("plain text, not a pdf"),
  });
  await page.getByTestId(testIds.uploadSubmit).click();

  await expect(page.getByTestId(testIds.uploadFileError)).toBeVisible();
  await expect(page).toHaveURL(routes.contracts);
});

test("shows the no-text message for an image-only PDF", async ({ page }) => {
  await page.goto(routes.contracts);
  await page.getByTestId(testIds.uploadTitleInput).fill("Scanned");
  await page.getByTestId(testIds.uploadFileInput).setInputFiles({
    name: "scan.pdf",
    mimeType: "application/pdf",
    buffer: buildPdf(null),
  });
  await page.getByTestId(testIds.uploadSubmit).click();

  await expect(page.getByTestId(testIds.uploadFileError)).toBeVisible();
  await expect(page).toHaveURL(routes.contracts);
});

test("shows the not-found state with a link back for an unknown contract", async ({ page }) => {
  await page.goto(routes.contractDetail("00000000-0000-4000-8000-000000000000"));

  await expect(page.getByTestId(testIds.contractNotFound)).toBeVisible();
  await expectNoSeriousViolations(page);
  await page.getByTestId(testIds.contractNotFoundBack).click();
  await expect(page).toHaveURL(routes.contracts);
});

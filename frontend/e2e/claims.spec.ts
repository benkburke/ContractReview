import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import { routes } from "../src/constants/routes";
import { testIds } from "../src/constants/testIds";
import { claimStrings } from "../src/strings/claims";

const LINES = [
  "SCHEDULE OF COVERAGE",
  "The engine is covered for five years.",
  "WHAT IS NOT COVERED",
  "Wear and tear is excluded.",
];
const CLAIM = "Is the engine covered?";

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

test("asking a claim on an indexed contract opens the results dashboard", async ({ page }) => {
  await page.goto(routes.contracts);
  await page.getByTestId(testIds.uploadTitleInput).fill(`Claim contract ${Date.now()}`);
  await page.getByTestId(testIds.uploadFileInput).setInputFiles({
    name: "warranty.pdf",
    mimeType: "application/pdf",
    buffer: buildPdf(LINES),
  });
  await page.getByTestId(testIds.uploadSubmit).click();
  await expect(page).toHaveURL(/\/contracts\/[0-9a-f-]{36}$/);

  await page.getByTestId(testIds.claimTextInput).fill(CLAIM);
  await page.getByTestId(testIds.claimSubmit).click();

  await expect(page).toHaveURL(/\/contracts\/[0-9a-f-]{36}\/claims\/[0-9a-f-]{36}$/);
  await expect(page.getByTestId(testIds.claimStatusBanner)).toContainText(claimStrings.status.Covered);
  await expect(page.getByTestId(testIds.claimConfidence)).toContainText("85");
  await expect(page.getByTestId(testIds.claimReasoning)).toBeVisible();
  await expect(page.getByTestId(testIds.claimCoveredItems)).toContainText("engine");
  await expect(page.getByTestId(testIds.claimCitationItem).first()).toContainText(LINES[0]);
  await expect(page.getByTestId(testIds.claimText)).toContainText(CLAIM);

  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"]).analyze();
  const serious = results.violations.filter((v) => v.impact === "serious" || v.impact === "critical");
  expect(serious.map((v) => `${v.id}: ${v.help}`)).toEqual([]);
});

test("submitting a claim shows live progress, then opens the result", async ({ page }) => {
  await page.goto(routes.contracts);
  await page.getByTestId(testIds.uploadTitleInput).fill(`Progress contract ${Date.now()}`);
  await page.getByTestId(testIds.uploadFileInput).setInputFiles({
    name: "warranty.pdf",
    mimeType: "application/pdf",
    buffer: buildPdf(LINES),
  });
  await page.getByTestId(testIds.uploadSubmit).click();
  await expect(page).toHaveURL(/\/contracts\/[0-9a-f-]{36}$/);

  // Hold the response after the server has handled the request so the pushed steps stay on screen.
  let release: () => void = () => {};
  const released = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route(/\/api\/contracts\/[0-9a-f-]{36}\/claims$/, async (route) => {
    if (route.request().method() !== "POST") return route.continue();
    const response = await route.fetch();
    await released;
    return route.fulfill({ response });
  });

  await page.getByTestId(testIds.claimTextInput).fill(CLAIM);
  await page.getByTestId(testIds.claimSubmit).click();

  const progress = page.getByTestId(testIds.claimProgress);
  await expect(progress).toBeVisible();
  await expect(page.getByTestId(testIds.claimProgressStep)).toHaveCount(4);

  release();
  await expect(page).toHaveURL(/\/contracts\/[0-9a-f-]{36}\/claims\/[0-9a-f-]{36}$/);
  await expect(page.getByTestId(testIds.claimStatusBanner)).toContainText(claimStrings.status.Covered);
});

test("the claim form is accessible while progress is shown", async ({ page }) => {
  await page.goto(routes.contracts);
  await page.getByTestId(testIds.uploadTitleInput).fill(`Busy contract ${Date.now()}`);
  await page.getByTestId(testIds.uploadFileInput).setInputFiles({
    name: "warranty.pdf",
    mimeType: "application/pdf",
    buffer: buildPdf(LINES),
  });
  await page.getByTestId(testIds.uploadSubmit).click();
  await expect(page).toHaveURL(/\/contracts\/[0-9a-f-]{36}$/);

  await page.route(/\/api\/contracts\/[0-9a-f-]{36}\/claims$/, async (route) => {
    if (route.request().method() !== "POST") return route.continue();
    const response = await route.fetch();
    await new Promise(() => {});
    return route.fulfill({ response });
  });
  await page.getByTestId(testIds.claimTextInput).fill(CLAIM);
  await page.getByTestId(testIds.claimSubmit).click();
  await expect(page.getByTestId(testIds.claimProgress)).toBeVisible();

  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"]).analyze();
  const serious = results.violations.filter((v) => v.impact === "serious" || v.impact === "critical");
  expect(serious.map((v) => `${v.id}: ${v.help}`)).toEqual([]);
});

async function uploadAndOpen(page: import("@playwright/test").Page, title: string) {
  await page.goto(routes.contracts);
  await page.getByTestId(testIds.uploadTitleInput).fill(`${title} ${Date.now()}`);
  await page.getByTestId(testIds.uploadFileInput).setInputFiles({
    name: "warranty.pdf",
    mimeType: "application/pdf",
    buffer: buildPdf(LINES),
  });
  await page.getByTestId(testIds.uploadSubmit).click();
  await expect(page).toHaveURL(/\/contracts\/[0-9a-f-]{36}$/);
}

async function expectNoSeriousViolations(page: import("@playwright/test").Page) {
  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"]).analyze();
  const serious = results.violations.filter((v) => v.impact === "serious" || v.impact === "critical");
  expect(serious.map((v) => `${v.id}: ${v.help}`)).toEqual([]);
}

test("a disputed claim shows the disagreement banner and its button expands and focuses the review panel", async ({ page }) => {
  await uploadAndOpen(page, "Disputed contract");
  await page.getByTestId(testIds.claimTextInput).fill("Is the engine covered? This is disputed");
  await page.getByTestId(testIds.claimSubmit).click();
  await expect(page).toHaveURL(/\/contracts\/[0-9a-f-]{36}\/claims\/[0-9a-f-]{36}$/);

  const banner = page.getByTestId(testIds.claimReviewDisagreeBanner);
  await expect(banner).toContainText(claimStrings.review.disagreeBanner);
  const summary = page.getByTestId(testIds.claimReviewSummary);
  await expect(summary).toContainText(claimStrings.review.heading);
  await expect(summary).toHaveAttribute("aria-expanded", "false");
  await expectNoSeriousViolations(page);

  await page.getByTestId(testIds.claimReviewDisagreeAction).click();
  await expect(summary).toHaveAttribute("aria-expanded", "true");
  await expect(summary).toBeFocused();
  await expect(page.getByTestId(testIds.claimReviewAgreement)).toContainText(claimStrings.review.disagrees);
  await expect(page.getByTestId(testIds.claimReviewModel)).toBeVisible();
  await expectNoSeriousViolations(page);
});

test("an unreviewable claim keeps its result and says the review was unavailable", async ({ page }) => {
  await uploadAndOpen(page, "Unreviewable contract");
  await page.getByTestId(testIds.claimTextInput).fill("Is the engine covered? This is unreviewable");
  await page.getByTestId(testIds.claimSubmit).click();
  await expect(page).toHaveURL(/\/contracts\/[0-9a-f-]{36}\/claims\/[0-9a-f-]{36}$/);

  await expect(page.getByTestId(testIds.claimStatusBanner)).toBeVisible();
  await expect(page.getByTestId(testIds.claimReviewDisagreeBanner)).toHaveCount(0);
  await page.getByTestId(testIds.claimReviewSummary).click();
  await expect(page.getByTestId(testIds.claimReviewNote)).toContainText(claimStrings.review.unavailable);
  await expectNoSeriousViolations(page);
});

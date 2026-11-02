# Coding Challenge: Extended Warranty Contract Evaluator

## Objective
Build a full-stack application that ingests an extended warranty contract, allows users to query their coverage based on specific incidents, and evaluates the quality and accuracy of the AI-generated responses.

## Tech Stack
*   **Backend:** .NET 10
*   **Frontend:** Next.js 16
*   **Architecture:** Clean Architecture (Domain, Application, Infrastructure, Presentation/API)
*   **Target File:** `Allstate-VSC-Extended-Sample.pdf`

---

## Phase 1: PDF Ingestion and Querying

### Requirements
1.  **PDF Upload:** Create an endpoint and frontend interface to upload the sample warranty contract (`Allstate-VSC-Extended-Sample.pdf`).
2.  **Document Parsing:** Extract the text and structure from the PDF, paying special attention to the "SCHEDULE OF COVERAGE" and "WHAT IS NOT COVERED" sections.
3.  **Query Interface:** Build a chat-like interface where a user can ask natural language questions about their coverage.
4.  **Test Scenarios:** The system must accurately answer the following test queries based on the provided PDF:
    *   *"My tires went flat. Is this covered?"*
    *   *"My air conditioner is broken. Is it covered?"*

### Architecture Constraints
*   Ensure the PDF parsing logic is isolated in the Infrastructure layer.
*   Query handling and LLM orchestration should be managed in the Application layer using appropriate interfaces.

---

## Phase 2: Response Evaluation Engine

### Requirements
Design an evaluation system that scores the answers provided in Phase 1. For every answer generated, the system must output an evaluation object containing:

1.  **Confidence Score (0-100%):** How confident is the system in its answer based on the retrieved text?
2.  **Coverage Status (Boolean):** A definitive `true` or `false` on whether the specific incident is actually covered under the contract.
3.  **Accuracy Evaluation:** A brief explanation validating why the evaluation engine agrees or disagrees with the primary response.
4.  **Coverage Breakdown:** A structured summary explicitly stating:
    *   **What is covered:** (e.g., Air conditioning compressor, condenser)
    *   **What is NOT covered:** (e.g., Tires, wheels, regular maintenance)

### Implementation Details
*   Create a dedicated Evaluation module within the .NET backend.
*   Update the Next.js frontend to display these evaluation metrics alongside the chat responses, perhaps in an expandable "Debug/Evaluation" panel.

---
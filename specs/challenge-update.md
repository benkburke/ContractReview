# Objective
Build a Warranty Claim Analysis Engine that ingests a PDF contract and evaluates natural language customer claims (e.g., "My air conditioner is broken") against the contract terms.

# Execution Context
* **Orchestration:** Utilize parallel subagents to implement the backend analysis pipeline and frontend evaluation dashboard simultaneously.
* **Backend:** .NET 10 Minimal APIs, C#, Clean Architecture (Domain, Application, Infrastructure, API).
* **Frontend:** Next.js 16 (App Router), TypeScript, Material UI (@mui/material, @mui/material-nextjs).
* **AI Integration:** Use semantic search (e.g., simple vector embedding or BM25) and an LLM integration (Semantic Kernel or direct API), such as openrouter, for the reasoning engine.

# Phase 1: Backend Architecture & PDF Processing
Assign a backend agent to construct the API and Infrastructure layers:
1. **Infrastructure - PDF Extraction:** Implement a service to parse PDF text (e.g., referencing "Allstate-VSC-Extended-Sample.pdf"). Ensure the extraction preserves logical groupings (e.g., "SCHEDULE OF COVERAGE", "WHAT IS NOT COVERED").
2. **Infrastructure - AI Reasoning Service:** Create an LLM service wrapper that takes the parsed contract text, the customer's coverage level, and the customer's query.
3. **Application - RAG Pipeline:** Implement a MediatR command or direct service that:
   * Receives the claim text.
   * Retrieves relevant contract chunks.
   * Prompts the LLM with a strict system prompt to evaluate the claim.
4. **Domain - Evaluation Models:** Define the structured output model `ClaimEvaluationResult`:
   * `IsCovered` (boolean or "Conditional")
   * `ConfidenceScore` (0-100)
   * `CoveredItems` (string array of what parts apply)
   * `ExcludedItems` (string array of relevant exclusions triggered)
   * `Reasoning` (string explaining the decision based on exact clauses)
5. **API - Endpoints:** Expose a .NET 10 Minimal API endpoint: `POST /api/claims/evaluate` accepting a PDF file and a text claim.

# Phase 2: Frontend Evaluation Dashboard
Assign a frontend agent to build the Next.js 16 UI:
1. **Layout & Theme:** Configure `@mui/material-nextjs` AppRouterCacheProvider to ensure strict SSR compliance without hydration mismatches.
2. **Upload Component:** Build an atomic component for uploading the warranty PDF and inputting the claim text.
3. **Results View:** Build a dashboard to display the evaluation:
   * A prominent indicator for Coverage Status (Covered, Not Covered, Needs More Info).
   * A progress bar or circular gauge for the Confidence Score.
   * Card components detailing the "Reasoning", "Relevant Coverage Clauses", and "Relevant Exclusions".

# LLM Prompt Engineering Constraints
The AI Reasoning Service must utilize a system prompt that enforces strict adherence to the contract text. Instruct the LLM to:
* Deny coverage if the part is not explicitly listed in the inclusions for the provided coverage tier.
* Deny coverage if a specific exclusion applies (e.g., maintenance neglect, waiting period).
* State "Insufficient Information" if the claim is too vague (e.g., "My car sounds weird") and prompt the user for specific diagnostic details.

# Definition of Done
* The .NET 10 backend successfully parses a PDF and returns a structured JSON evaluation based on the LLM's analysis of the text.
* The Next.js frontend allows users to submit claims and clearly visualizes the evaluation quality, confidence, and accuracy metrics.
* Strict separation of concerns is maintained across the Clean Architecture boundaries.
import createClient from "openapi-fetch";
import { config } from "@/config";

// Add the generated `paths` type as the generic once src/api/generated exists (npm run gen:api).
export const apiClient = createClient({ baseUrl: config.apiUrl });

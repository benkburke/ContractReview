import createClient from "openapi-fetch";
import { config } from "@/config";
import type { paths } from "@/api/generated/schema";

export const apiClient = createClient<paths>({ baseUrl: config.apiUrl });

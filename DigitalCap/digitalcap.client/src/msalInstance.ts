import { PublicClientApplication, type Configuration, type IPublicClientApplication } from "@azure/msal-browser";
import axios, { type AxiosResponse } from "axios";
import { isElectron } from "./utils/runtime";
interface AppConfig {
  azureAdConfig: Configuration;
  azureAdMsalConfig?: Configuration;
}

interface ConfigModule {
  default: AppConfig;
}

const loadLocalConfig = async (): Promise<AppConfig> => {
  const configModule = await import(`./config.local.json`) as ConfigModule;
  return configModule.default;
};

const loadConfig = async (): Promise<AppConfig> => {
  const environment: string = import.meta.env.VITE_APP_ENV || import.meta.env.REACT_APP_ENV || "local";

  // The desktop app must start without a network, so it always uses the bundled config.
  if (environment.toLowerCase() === "local" || isElectron()) {
    return loadLocalConfig();
  }

  try {
    const proxyUrl: string = import.meta.env.VITE_APP_PROXY_URL;
    const response: AxiosResponse<AppConfig> = await axios.get(
      `${proxyUrl}/api/Auth/GetAuthConfig`
    );
    return response.data;
  } catch {
    return loadLocalConfig();
  }
};

let msalInstance: IPublicClientApplication | null = null;

const initializeMsal = async (): Promise<IPublicClientApplication> => {
  if (msalInstance) {
    return msalInstance;
  }

  try {
    let currentPath = window.location.pathname;
    if (
      !localStorage.getItem("authConfig") ||
      currentPath === "/" ||
      currentPath.toLowerCase().includes("/internal")
    ) {
      sessionStorage.clear();
      const cookies = document.cookie.split(";");
      cookies.forEach((cookie) => {
        const name = cookie.split("=")[0].trim();
        document.cookie = `${name}=;expires=Thu, 01 Jan 1970 00:00:00 UTC;path=/;`;
      });

      const authTypeValue = currentPath.toLowerCase().includes("/internal") ? "internal" : "external";
      localStorage.setItem("authConfig", authTypeValue);
    }

    const AppConfig: AppConfig = await loadConfig();
    const msalConfig: Configuration = AppConfig.azureAdConfig;

    if (msalConfig && msalConfig.auth) {
        msalConfig.auth.redirectUri = `${window.location.origin}/success`;
        msalConfig.auth.postLogoutRedirectUri = `${window.location.origin}/`;
    }

    msalInstance = new PublicClientApplication(msalConfig);
    await msalInstance.initialize();
  } catch (error) {
    throw error;
  }

  return msalInstance;
};

export default initializeMsal;
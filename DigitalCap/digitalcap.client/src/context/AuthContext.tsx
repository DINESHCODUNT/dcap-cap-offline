import { createContext, useEffect, useState, type ReactNode } from "react";
import { useMsal } from "@azure/msal-react";
import { InteractionStatus } from "@azure/msal-browser";
import { type User } from "../types/auth";
import { getApiScopes } from "../utils/authConfig";
import { getOfflineUser, isElectron } from "../utils/runtime";

const getOfflineSessionUser = async (): Promise<User | null> => {
  const offlineUser = await getOfflineUser();
  if (!offlineUser) return null;

  const name = [offlineUser.firstName, offlineUser.lastName].filter(Boolean).join(" ");
  // Shaped like the MSAL account the rest of the app stores, so existing readers keep working.
  return {
    name: name || offlineUser.emailId,
    username: offlineUser.emailId,
    localAccountId: offlineUser.id,
    homeAccountId: offlineUser.id,
    roles: [],
    isOffline: true,
  } as User;
};

interface AuthContextType {
  user: User | null;
  loading: boolean;
}

export const AuthContext = createContext<AuthContextType>({
  user: null,
  loading: true,
});

export const AuthProvider = ({ children }: { children: ReactNode }) => {
  const { instance, accounts, inProgress } = useMsal();
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchUser = async () => {
      setLoading(true);

      // In desktop mode:
      // If a user exists in LogInData (the last user who signed in online on this computer)
      // -> go straight to the project list as that user, online or offline. No Azure prompt, no typing.
      if (isElectron()) {
        try {
          const offlineUser = await getOfflineSessionUser();
          if (offlineUser) {
            setUser(offlineUser);
            localStorage.setItem("user", JSON.stringify(offlineUser));
            setLoading(false);
            return;
          }
        } catch { }
      }

      if (inProgress !== InteractionStatus.None) {
        return;
      }

      try {
        if (accounts.length > 0) {
          const account = accounts[0];

          try {
            const apiScopes = await getApiScopes();
            const tokenResponse = await instance.acquireTokenSilent({
              scopes: apiScopes,
              account: account,
            });

            if (tokenResponse.accessToken) {
              localStorage.setItem("token", tokenResponse.accessToken);
              localStorage.setItem("user", JSON.stringify(account));
              setUser(account as any);
            }
          } catch (tokenError) {
            setUser(null);
          }
        } else {
          setUser(null);
        }
      } catch (err) {
        setUser(null);
      } finally {
        setLoading(false);
      }
    };

    fetchUser();
  }, [accounts, instance, inProgress]);

  return (
    <AuthContext.Provider value={{ user, loading }}>
      {children}
    </AuthContext.Provider>
  );
};
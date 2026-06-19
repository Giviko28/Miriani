import { useState } from "react";
import { AuthProvider, useAuth } from "./auth";
import { BrandingProvider } from "./branding";
import { Landing } from "./views/Landing";
import { Login } from "./views/Login";
import { Chat } from "./views/Chat";
import { ChangePassword } from "./views/ChangePassword";
import { AdminApp } from "./admin/AdminApp";

const ADMIN_ROLE = 2;

function Shell() {
  const { session } = useAuth();
  const [showLogin, setShowLogin] = useState(false);

  if (!session) {
    return showLogin ? <Login onBack={() => setShowLogin(false)} /> : <Landing onLogin={() => setShowLogin(true)} />;
  }
  if (session.mustChangePassword) return <ChangePassword forced />;
  if (session.role === ADMIN_ROLE) return <AdminApp />;
  return <Chat />;
}

export default function App() {
  return (
    <AuthProvider>
      <BrandingProvider>
        <Shell />
      </BrandingProvider>
    </AuthProvider>
  );
}

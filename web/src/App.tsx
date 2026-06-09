import { AuthProvider, useAuth } from "./auth";
import { BrandingProvider } from "./branding";
import { Login } from "./views/Login";
import { Chat } from "./views/Chat";
import { ChangePassword } from "./views/ChangePassword";
import { AdminApp } from "./admin/AdminApp";

const ADMIN_ROLE = 2;

function Shell() {
  const { session } = useAuth();

  if (!session) return <Login />;
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

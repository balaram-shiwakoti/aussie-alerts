import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Link, Navigate, Outlet, Route, Routes } from 'react-router-dom';
import { useSession, setSession } from './session';
import { AuthPage } from './pages/AuthPage';
import { AlertsPage } from './pages/AlertsPage';
import { ListingsPage } from './pages/ListingsPage';
import { InboxPage } from './pages/InboxPage';
function Protected() { return useSession() ? <Outlet/> : <Navigate to="/login" replace/>; }
export default function App() {
  const session = useSession(); const cache = useQueryClient();
  useEffect(() => { if (!session) cache.clear(); }, [session, cache]);
  useEffect(() => {
    if (!session) return;
    const timer = window.setTimeout(() => setSession(null), Math.max(0, Date.parse(session.expiresAt) - Date.now()));
    return () => window.clearTimeout(timer);
  }, [session]);
  return <><header><Link className="brand" to="/alerts">Aussie<span>Alerts</span></Link><nav aria-label="Main navigation">
    {session ? <><Link to="/alerts">Alerts</Link><Link to="/listings">Listings</Link><Link to="/inbox">Inbox</Link><button className="secondary" onClick={() => {setSession(null); cache.clear();}}>Log out</button></>
      : <><Link to="/login">Log in</Link><Link to="/register">Register</Link></>}
  </nav></header><div className="notice">Learning project · Fake Australian listings only · No real email delivery</div><main>
    <Routes><Route path="/login" element={<AuthPage key="login"/>}/><Route path="/register" element={<AuthPage key="register" register/>}/>
      <Route element={<Protected/>}><Route path="/alerts" element={<AlertsPage/>}/><Route path="/listings" element={<ListingsPage/>}/><Route path="/inbox" element={<InboxPage/>}/></Route>
      <Route path="*" element={<Navigate to="/alerts" replace/>}/></Routes>
  </main><footer>C# / ASP.NET Core · React + TypeScript · PostgreSQL</footer></>;
}

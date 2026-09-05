import { useEffect, useRef, useState } from "react";
import { HashRouter, Routes, Route, Link, NavLink, useLocation, useNavigate } from "react-router-dom";
import { fetchServerInfo, type ServerInfo } from "./api/serverInfo";
import { AuthProvider, useAuth } from "./state/AuthContext";
import { RequireAuth } from "./components/RequireAuth";
import { LoginPage } from "./pages/LoginPage";
import { DashboardPage } from "./pages/DashboardPage";
import { QuestionBankListPage } from "./pages/QuestionBankListPage";
import { QuestionBankEditorPage } from "./pages/QuestionBankEditorPage";
import { RosterListPage } from "./pages/RosterListPage";
import { RosterEditorPage } from "./pages/RosterEditorPage";
import { MatchSetupPage } from "./pages/MatchSetupPage";
import { LiveMatchMonitorPage } from "./pages/LiveMatchMonitorPage";
import { LeaderboardPage } from "./pages/LeaderboardPage";

function ConnectionStatus() {
  const [info, setInfo] = useState<ServerInfo | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    fetchServerInfo().then((value) => { if (!cancelled) setInfo(value); })
      .catch((e) => { if (!cancelled) setError(String(e)); });
    return () => { cancelled = true; };
  }, []);

  return (
    <div className="app-connection" role="status" title={error ?? info?.serverName}>
      <span className={`app-status-dot ${error ? "is-offline" : info ? "is-online" : ""}`} aria-hidden="true" />
      <div><strong>{error ? "Server unavailable" : info ? "Server available" : "Checking server..."}</strong>
        <span>{info ? `${info.mode.toUpperCase()} / v${info.version}` : "Classroom connection"}</span></div>
    </div>
  );
}

const navigation = [
  { title: "Your workspace", links: [
    { to: "/", label: "Dashboard", icon: "M3 3h7v7H3zM14 3h7v7h-7zM3 14h7v7H3zM14 14h7v7h-7z" },
    { to: "/rosters", label: "Classes", icon: "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M16 3a4 4 0 0 1 0 8M22 21v-2a4 4 0 0 0-3-3.87M13 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0Z" },
    { to: "/question-banks", label: "Question banks", icon: "M4 3h6a3 3 0 0 1 3 3v15a3 3 0 0 0-3-3H4zM13 6a3 3 0 0 1 3-3h5v15h-5a3 3 0 0 0-3 3" },
  ] },
  { title: "Game day", links: [
    { to: "/match-setup", label: "Match setup", icon: "m9 3 12 9-12 9zM3 4v16" },
    { to: "/live-match", label: "Live match", icon: "M3 4h18v13H3zM8 21h8M12 17v4m-7-11 4 4 3-6 3 4h4" },
    { to: "/leaderboard", label: "Leaderboard", icon: "M8 3h8v6a4 4 0 0 1-8 0zM8 5H4v2a4 4 0 0 0 4 4M16 5h4v2a4 4 0 0 1-4 4M12 13v5M8 21h8M10 18h4" },
  ] },
];

function Brand() {
  return <Link className="app-brand" to="/" aria-label="QuizBattle dashboard">
    <svg viewBox="0 0 40 44" fill="none" aria-hidden="true"><path d="M3 3h34v23L20 41 3 26Z" fill="currentColor" /><path d="m22 9-11 15h8l-1 11 12-17h-9z" fill="#16252b" /></svg>
    <span>QuizBattle<small>TEACHER STUDIO</small></span>
  </Link>;
}

function SiteLayout() {
  const { isAuthenticated, teacher, signOut } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [menuOpen, setMenuOpen] = useState(false);
  const mainRef = useRef<HTMLElement>(null);
  const menuRef = useRef<HTMLButtonElement>(null);
  const previousPath = useRef(location.pathname);
  const studio = isAuthenticated && location.pathname !== "/login";
  const activePage = navigation.flatMap((group) => group.links).find((link) => link.to === "/" ? location.pathname === "/" : location.pathname.startsWith(link.to));

  useEffect(() => {
    if (previousPath.current !== location.pathname) {
      previousPath.current = location.pathname;
      mainRef.current?.focus({ preventScroll: true });
    }
    document.title = `${location.pathname === "/login" ? "Welcome" : activePage?.label ?? "Page not found"} | QuizBattle`;
  }, [location.pathname, activePage?.label]);

  function onLogout() {
    signOut();
    setMenuOpen(false);
    navigate("/login");
  }

  return (
    <div className={studio ? "app-shell" : "app-shell app-shell-public"}>
      <a className="skip-link" href="#main-content" onClick={(event) => { event.preventDefault(); mainRef.current?.focus(); }}>Skip to content</a>
      {studio ? <>
        <header className="app-mobile-header" onKeyDown={(event) => { if (event.key === "Escape") { setMenuOpen(false); menuRef.current?.focus(); } }}><Brand /><button ref={menuRef} className="app-menu-button" aria-expanded={menuOpen} aria-controls="studio-navigation" onClick={() => setMenuOpen(!menuOpen)}>{menuOpen ? "Close menu" : "Menu"}</button></header>
        <aside className={`app-sidebar ${menuOpen ? "is-open" : ""}`} id="studio-navigation" onKeyDown={(event) => { if (event.key === "Escape") { setMenuOpen(false); menuRef.current?.focus(); } }}>
          <div className="app-sidebar-brand"><Brand /></div>
          <nav aria-label="Main navigation">
            {navigation.map((group) => <div className="app-nav-group" key={group.title}>
              <p className="app-nav-label">{group.title}</p>
              {group.links.map((link) => <NavLink key={link.to} to={link.to} end={link.to === "/"} onClick={() => { setMenuOpen(false); mainRef.current?.focus({ preventScroll: true }); }} className={({ isActive }) => `app-nav-link ${isActive ? "is-active" : ""}`}>
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d={link.icon} /></svg>{link.label}
              </NavLink>)}
            </div>)}
          </nav>
          <div className="app-sidebar-note"><span className="app-nav-label">The classroom, recharged.</span><p>Good questions.<br />Great competition.</p><a href="/play/">Open student game <span aria-hidden="true">&#8599;</span></a></div>
          <div className="app-sidebar-footer"><ConnectionStatus /><div className="app-profile"><span className="app-avatar" aria-hidden="true">{teacher?.displayName?.trim().slice(0, 1).toUpperCase() || "T"}</span><div><strong>{teacher?.displayName ?? "Teacher"}</strong><span>Teacher account</span></div><button onClick={onLogout} className="app-logout">Log out</button></div></div>
        </aside>
      </> : <header className="app-public-header"><Brand /><a href="/play/">Here to play? <strong>Student game <span aria-hidden="true">&#8599;</span></strong></a></header>}
      <div className="app-workspace">
        {studio && <nav className="app-topbar" aria-label="Breadcrumb"><span><Link to="/">Teacher studio</Link> <span aria-hidden="true">/</span> <strong aria-current="page">{activePage?.label ?? "Explore"}</strong></span><span className="app-topbar-tag">Made for a room full of potential</span></nav>}
        <main id="main-content" ref={mainRef} tabIndex={-1}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/" element={<RequireAuth><DashboardPage /></RequireAuth>} />
          <Route path="/question-banks" element={<RequireAuth><QuestionBankListPage /></RequireAuth>} />
          <Route path="/question-banks/:id" element={<RequireAuth><QuestionBankEditorPage /></RequireAuth>} />
          <Route path="/rosters" element={<RequireAuth><RosterListPage /></RequireAuth>} />
          <Route path="/rosters/:id" element={<RequireAuth><RosterEditorPage /></RequireAuth>} />
          <Route path="/match-setup" element={<RequireAuth><MatchSetupPage /></RequireAuth>} />
          <Route path="/live-match" element={<RequireAuth><LiveMatchMonitorPage /></RequireAuth>} />
          <Route path="/leaderboard" element={<RequireAuth><LeaderboardPage /></RequireAuth>} />
          <Route path="*" element={<div className="page"><p className="eyebrow">Out of bounds</p><h1>That page isn't in the game plan.</h1><p>Head back to your dashboard to start your next round.</p><Link className="btn btn-primary" to="/">Back to dashboard</Link></div>} />
        </Routes>
        </main>
        <footer className="app-footer"><span>QUIZBATTLE</span><span>Play with purpose. Learn together.</span></footer>
      </div>
    </div>
  );
}

function App() {
  return (
    <AuthProvider>
      <HashRouter>
        <SiteLayout />
      </HashRouter>
    </AuthProvider>
  );
}

export default App;

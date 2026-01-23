import React, { useState, useEffect } from 'react';
import './App.css';
import Login from './components/Login';
import Dashboard from './components/Dashboard';
import { getToken, setToken, clearToken } from './utils/auth';

function App() {
  const [authenticated, setAuthenticated] = useState(false);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const token = getToken();
    if (token) {
      setAuthenticated(true);
    }
    setLoading(false);
  }, []);

  const handleLogin = (token) => {
    setToken(token);
    setAuthenticated(true);
  };

  const handleLogout = () => {
    clearToken();
    setAuthenticated(false);
  };

  if (loading) {
    return <div className="loading">Loading...</div>;
  }

  return (
    <div className="App">
      {authenticated ? (
        <Dashboard onLogout={handleLogout} />
      ) : (
        <Login onLogin={handleLogin} />
      )}
    </div>
  );
}

export default App;


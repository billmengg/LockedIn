import React, { useState } from 'react';
import './Login.css';
import { login, register } from '../utils/api';

function Login({ onLogin }) {
  const [isRegister, setIsRegister] = useState(false);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      if (isRegister) {
        await register(email, password);
        const result = await login(email, password);
        onLogin(result.token);
      } else {
        const result = await login(email, password);
        onLogin(result.token);
      }
    } catch (err) {
      setError(err.response?.data?.error || 'An error occurred');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-container">
      <div className="login-box">
        <h1>Accountability</h1>
        <p className="subtitle">Parent Portal</p>

        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label>Email or Username</label>
            <input
              type="text"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              disabled={loading}
            />
          </div>

          <div className="form-group">
            <label>Password</label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              disabled={loading}
              minLength={6}
            />
          </div>

          {error && <div className="error">{error}</div>}

          <button type="submit" disabled={loading} className="primary-button">
            {loading ? 'Please wait...' : isRegister ? 'Register' : 'Login'}
          </button>
        </form>

        <button
          type="button"
          onClick={() => setIsRegister(!isRegister)}
          className="link-button"
        >
          {isRegister
            ? 'Already have an account? Login'
            : "Don't have an account? Register"}
        </button>
      </div>
    </div>
  );
}

export default Login;


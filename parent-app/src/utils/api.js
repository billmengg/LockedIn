import axios from 'axios';
import { getToken } from './auth';

const API_BASE_URL = process.env.REACT_APP_API_URL || 'http://localhost:5000';

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

api.interceptors.request.use((config) => {
  const token = getToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export const register = async (email, password) => {
  const response = await api.post('/api/auth/register', { email, password });
  return response.data;
};

export const login = async (email, password) => {
  const response = await api.post('/api/auth/login', { email, password });
  return response.data;
};

export const pairDevice = async (pairingCode) => {
  const response = await api.post('/api/pair', { pairingCode });
  return response.data;
};

export const getDevices = async () => {
  const response = await api.get('/api/devices');
  return response.data;
};

export default api;


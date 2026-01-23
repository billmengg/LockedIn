const STORAGE_KEY = 'accountability_paired_devices';

export const loadPairedDevices = () => {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw);
    if (!Array.isArray(parsed)) return [];
    return parsed.filter((d) => d && d.id);
  } catch {
    return [];
  }
};

export const savePairedDevices = (devices) => {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(devices));
  } catch {
    // ignore storage errors
  }
};

export const upsertPairedDevice = (device) => {
  const existing = loadPairedDevices();
  const filtered = existing.filter((d) => d.id !== device.id);
  const updated = [device, ...filtered];
  savePairedDevices(updated);
  return updated;
};

export const updateDevicePairingCode = (deviceId, pairingCode) => {
  const existing = loadPairedDevices();
  const updated = existing.map((d) => {
    if (d.id === deviceId) {
      return { ...d, pairingCode };
    }
    return d;
  });
  savePairedDevices(updated);
  return updated;
};

export const updateDeviceName = (deviceId, name) => {
  const existing = loadPairedDevices();
  const updated = existing.map((d) => {
    if (d.id === deviceId) {
      return { ...d, name };
    }
    return d;
  });
  savePairedDevices(updated);
  return updated;
};

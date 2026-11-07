const TIME_ZONE = 'America/El_Salvador';

const dateFormat = new Intl.DateTimeFormat('es-SV', {
  timeZone: TIME_ZONE,
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
});

const dateTimeFormat = new Intl.DateTimeFormat('es-SV', {
  timeZone: TIME_ZONE,
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
});

export const formatDate = (value) => (value ? dateFormat.format(new Date(value)) : '—');

export const formatDateTime = (value) => (value ? dateTimeFormat.format(new Date(value)) : '—');

export const formatOnlyDate = (value) => {
  if (!value) return '—';
  const [year, month, day] = value.split('-');
  return `${day}/${month}/${year}`;
};

export const formatNumber = (value, decimals = 2) =>
  Number(value ?? 0).toLocaleString('en-US', {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  });

export const formatFactor = (value) => (value === null || value === undefined ? '—' : formatNumber(value, 6));

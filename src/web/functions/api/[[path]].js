export async function onRequest(context) {
  const origin = context.env.API_ORIGIN || 'https://siphon-3js4.onrender.com';
  const url = new URL(context.request.url);
  const req = new Request(origin + url.pathname + url.search, context.request);
  req.headers.set('host', new URL(origin).host);
  if (context.env.WEB_API_KEY) req.headers.set('X-Api-Key', context.env.WEB_API_KEY);
  return fetch(req);
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (url.pathname.startsWith('/api/')) {
      const origin = env.API_ORIGIN || 'https://siphon-api.muqimjon.uz';
      const req = new Request(origin + url.pathname + url.search, request);
      req.headers.set('host', new URL(origin).host);
      if (env.WEB_API_KEY) req.headers.set('X-Api-Key', env.WEB_API_KEY);
      return fetch(req);
    }
    const res = await env.ASSETS.fetch(request);
    if (res.status === 404 && (request.headers.get('accept') || '').includes('text/html')) {
      return env.ASSETS.fetch(new Request(new URL('/', request.url), request));
    }
    return res;
  },
};

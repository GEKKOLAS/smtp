const EMAIL_WIDTH = 600;

function withHead(html: string, head: string) {
  if (/<head[^>]*>/i.test(html)) return html.replace(/<head[^>]*>/i, (tag) => `${tag}${head}`);
  if (/<html[^>]*>/i.test(html)) return html.replace(/<html[^>]*>/i, (tag) => `${tag}<head>${head}</head>`);
  return `<!doctype html><html><head>${head}</head><body>${html}</body></html>`;
}

/** A received message, fitted to the phone: fixed-width tables and images shrink to the screen. */
export function readableEmail(html: string) {
  return withHead(
    html,
    `<meta name="viewport" content="width=device-width, initial-scale=1">` +
      `<style>body{margin:0;padding:12px;word-wrap:break-word}img{max-width:100%!important;height:auto!important}` +
      `table{max-width:100%!important}</style>`,
  );
}

/** A template at its real 600px width, zoomed down so it fills a `width`-wide thumbnail. */
export function thumbnailEmail(html: string, width: number) {
  return withHead(
    html,
    `<meta name="viewport" content="width=device-width, initial-scale=1">` +
      `<style>html{zoom:${(width / EMAIL_WIDTH).toFixed(4)};overflow:hidden}body{width:${EMAIL_WIDTH}px}</style>`,
  );
}

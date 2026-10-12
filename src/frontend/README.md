# MiniERP web (Angular 19)

Standalone components, signals, Angular Material 3 and a typed API client.

```bash
npm install
npm start          # http://localhost:4200, proxies /api to http://localhost:5080 (see proxy.conf.json)
npm run test:ci    # unit tests in headless Chrome
npx ng build       # production build -> dist/web/browser
```

| Folder | Contents |
|---|---|
| `src/app/core` | models, API client, auth (signals), interceptors and guards, GST preview maths |
| `src/app/layout` | app shell: sidenav, toolbar, theme toggle |
| `src/app/features` | dashboard, invoices (list / editor / detail), products, customers, team, auth |
| `src/app/shared` | INR pipe, status chip, page header |

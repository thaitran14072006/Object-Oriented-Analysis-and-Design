export default ({ command }) => {
  const backend = process.env.RENTALPRO_DEV_BACKEND || 'http://localhost:5080'
  return {
    base: command === 'build' ? '/frontend/' : '/',
    server: {
      proxy: {
        '/api': backend,
        '/identity/external': backend,
        '/settings/external': backend,
        '/signin-google': backend,
      },
    },
    build: {
      outDir: '../wwwroot/frontend',
      emptyOutDir: true,
    },
  }
}

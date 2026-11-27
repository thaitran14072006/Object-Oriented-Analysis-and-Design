export default ({ command }) => ({
  base: command === 'build' ? '/frontend/' : '/',
  build: {
    outDir: '../wwwroot/frontend',
    emptyOutDir: true,
  },
})

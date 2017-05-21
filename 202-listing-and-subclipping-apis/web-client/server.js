// jshint esversion:6
const restify = require('restify');
const path = require('path');
const server = restify.createServer();

server.get('/config.json', (req, res, next) => {
    var config = {
        baseUrl: process.env.API_BASE_URL
    };
    res.json(200, config);
});

server.get(/\/?.*/, restify.serveStatic({
    directory: path.join(__dirname, 'static'),
    default: 'index.html'
}));

server.listen(process.env.PORT || 8080, () => {
    console.log("server up");
});

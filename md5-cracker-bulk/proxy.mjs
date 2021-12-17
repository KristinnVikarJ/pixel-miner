import createError from 'http-errors';
import express from 'express';
import path from 'path';
import cookieParser from 'cookie-parser';
import morgan from 'morgan';
import { createClient } from 'redis';
import fetch from 'node-fetch';

var app = express();

const port = 13337

let redisClient = createClient();
redisClient.connect();

app.use(morgan(":date[iso] :remote-addr :method :url HTTP/:http-version :status :response-time ms :referrer :user-agent"));
app.use(express.json());
app.use(express.urlencoded({ extended: false }));
app.use(cookieParser());

const avgData = []
const avgAgeMax = 60 * 5 * 1000

function filterAndCalculateAverage(){
  const now = Date.now().valueOf()
  const filtered = avgData.filter(data => (now - data) < avgAgeMax)
  const sum = filtered.length
  const avg = sum / (60*5)
  return avg
}

app.post('/data', (req, res) => {
  const { session, pow } = req.body;
  redisClient.rPush("sessionqueue", JSON.stringify({ session, pow }));
  avgData.push(new Date().valueOf())
  res.send(`${session} - ${pow}`);
})

app.post('/bulkdata', (req, res) => {
  req.body.forEach(element => {
    avgData.push(new Date().valueOf())
    redisClient.rPush("sessionqueue", JSON.stringify({ 
      session: element.session, 
      pow: element.pow, 
      target: element.target }));
  });
  res.send("OK");
})

app.post('/set', async (req, res) => {
  req.body.forEach(element => {
    redisClient.rPush("workqueue", JSON.stringify({ 
      session: element.session, 
      salt: element.salt, 
      target: element.target }));
  });
  res.send("OK");
})

app.get('/getwork', async (req, res) => {
  const dat = []
  const length = await redisClient.lLen("workqueue");
  if(length >= 1024){
    for (let i = 0; i < 1024; i++) {
        const data = await redisClient.blPop("workqueue", 1);
        const element = JSON.parse(data.element);
        dat.push(element);
    }
    res.json(dat)
  }else{
    res.status(404).send("No work available: " + length)
  }
})

app.get('/worklength', async (req, res) => {
  const length = await redisClient.lLen("workqueue");
  res.json({length})
})

app.get('/status', async (req, res) => {
  const data = await redisClient.lLen("sessionqueue");
  res.json({count: data, avg: filterAndCalculateAverage()});
})

app.get('/get', async (req, res) => {
  const { count } = req.query;

  const reqData = await fetch("http://localhost:1338/queue")
  const pixelData = await reqData.json()
  try {
    const dat = []
    for (let i = 0; i < pixelData.length; i++) {
        const data = await redisClient.blPop("sessionqueue", 1);
        const element = JSON.parse(data.element);
        dat.push({
            session: element.session,
            pow: element.pow.replace(/\r?\n|\r/g, ""),
            pos: pixelData[i].pos,
            color: pixelData[i].color
          });
    }
    res.json(dat)
  }catch{
    res.json({})
  }
})

app.listen(port || 13337, () => {
  console.log(`Example app listening at http://localhost:${port}`)
})
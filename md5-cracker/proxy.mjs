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

app.get('/status', async (req, res) => {
  const data = await redisClient.lLen("sessionqueue");
  res.json({count: data, avg: filterAndCalculateAverage()});
})

app.get('/get', async (req, res) => {
  const { count } = req.query;

  const reqData = await fetch("http://localhost:1338/queue")
  const pixelData = await reqData.json()

  const dat = []
  for (let i = 0; i < 15; i++) {
      const data = await redisClient.blPop("sessionqueue", 1);
      const element = JSON.parse(data.element);
      dat.push({
          session: element.session,
          pow: element.pow,
          pos: pixelData[i].pos,
          color: pixelData[i].color
        });
  }
  res.json(dat)
})

app.listen(port || 13337, () => {
  console.log(`Example app listening at http://localhost:${port}`)
})
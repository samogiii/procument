// // client/server/api/test-pdf.get.ts
// import puppeteer from 'puppeteer';

// export default defineEventHandler(async (event) => {
//   const browser = await puppeteer.launch({ headless: true });
//   const page = await browser.newPage();
  
//   // Hardcode simple HTML just to test the engine
//   await page.setContent('<h1>It Works!</h1><p>Your Puppeteer GET method is working perfectly.</p>');

//   const pdfUint8Array = await page.pdf({ format: 'A4' });
//   await browser.close();

//   // Return the raw binary Response
//   return new Response(pdfUint8Array, {
//     status: 200,
//     headers: {
//       'Content-Type': 'application/pdf',
//       'Content-Disposition': 'attachment; filename="test-get.pdf"',
//       'Content-Length': pdfUint8Array.length.toString()
//     }
//   });
// });
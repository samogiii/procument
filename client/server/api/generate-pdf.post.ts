import puppeteer from 'puppeteer';

export default defineEventHandler(async (event) => {
  const body = await readBody(event);
  const htmlContent = body.html;

  if (!htmlContent) {
    throw createError({ statusCode: 400, statusMessage: 'HTML content is required' });
  }

  // 1. Launch the browser using your LOCAL Windows installation
  const browser = await puppeteer.launch({ 
    headless: true,
    // --- CHOOSE ONE OF THESE AND UNCOMMENT IT ---
    // For Google Chrome:
    executablePath: 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
    // For Microsoft Edge:
    // executablePath: 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
    
    args: ['--no-sandbox', '--disable-setuid-sandbox'] 
  });
  
  try {
    const page = await browser.newPage();

    // 2. Load the HTML into the browser
    await page.setContent(htmlContent, { waitUntil: 'networkidle0' });

    // 3. Generate the Vector PDF
    const pdfUint8Array = await page.pdf({
      format: 'A4',
      printBackground: true,
      margin: { top: '0', right: '0', bottom: '0', left: '0' }
    });

    // 4. Return the raw binary Response to bypass Nitro's JSON formatter
    return new Response(pdfUint8Array, {
      status: 200,
      headers: {
        'Content-Type': 'application/pdf',
        'Content-Disposition': 'attachment; filename="document.pdf"',
        'Content-Length': pdfUint8Array.length.toString()
      }
    });

  } catch (error) {
    // 5. Log the EXACT error to your terminal so you can see what failed
    console.error('Puppeteer Crash Details:', error);
    throw createError({ statusCode: 500, statusMessage: 'Failed to generate PDF' });
  } finally {
    // 6. Always close the browser, even if it crashes, to prevent memory leaks
    if (browser) {
      await browser.close();
    }
  }
});
import puppeteer from 'puppeteer-core';
import path from 'path';
import fs from 'fs';
import crypto from 'crypto';

export default defineEventHandler(async (event) => {
  const body = await readBody(event);
  
  if (!body.html) {
    throw createError({ statusCode: 400, statusMessage: 'HTML is required' });
  }

  const baseTempPath = 'C:\\PuppeteerTemp'; 
  if (!fs.existsSync(baseTempPath)) {
    fs.mkdirSync(baseTempPath, { recursive: true });
  }

  const customUserDataDir = path.join(baseTempPath, `profile_${crypto.randomUUID()}`);
  let browser;

  try {
    browser = await puppeteer.launch({
      headless: true,
      executablePath: 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
      // REMOVE userDataDir from here
      pipe: true, // Use pipes instead of WebSockets (Crucial for IIS)
      args: [
        `--user-data-dir=${customUserDataDir}`, // Pass it directly to Chrome to bypass Puppeteer's check
        '--no-sandbox',
        '--disable-setuid-sandbox',
        '--disable-dev-shm-usage',
        '--disable-gpu',
        '--no-zygote',
        '--password-store=basic', // Prevents Windows credential manager locks
        '--use-mock-keychain',
        '--disable-background-networking',
        '--disable-default-apps',
        '--disable-extensions',
        '--disable-sync',
        '--hide-scrollbars',
        '--mute-audio'
      ]
    });

    const page = await browser.newPage();
    await page.setContent(body.html, { waitUntil: 'networkidle0' });
    const pdf = await page.pdf({ format: 'A4', printBackground: true });

    return new Response(pdf, {
      status: 200,
      headers: { 
        'Content-Type': 'application/pdf',
        'Content-Disposition': 'inline; filename="document.pdf"'
      }
    });

  } catch (error) {
    console.error('Puppeteer Crash:', error);
    throw createError({ statusCode: 500, statusMessage: error.message });
  } finally {
    if (browser) {
      await browser.close();
      
      // Keep the cleanup logic to prevent disk space issues
      setTimeout(() => {
        try {
          if (fs.existsSync(customUserDataDir)) {
            fs.rmSync(customUserDataDir, { recursive: true, force: true });
          }
        } catch (e) {
          console.warn('Cleanup failed (expected if AV is scanning):', e.message);
        }
      }, 5000);
    }
  }
});
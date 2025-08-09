const { chromium } = require('@playwright/test');
const path = require('path');
const fs = require('fs');
const { spawn } = require('child_process');

// Configuration
const APP_URL = 'http://localhost:5113';
const SCREENSHOT_DIR = path.join(__dirname, '..', 'screenshots');
const VIEWPORT = { width: 1920, height: 1080 };
const TIMEOUT = 30000;

// Pages to test
const PAGES_TO_TEST = [
    { name: 'home', path: '/', waitFor: 'h1' },
    { name: 'duplicates', path: '/duplicates', waitFor: '.duplicate-finder-container, .container' },
    { name: 'finder', path: '/finder', waitFor: '.container' },
    { name: 'metadata', path: '/metadata', waitFor: '.container' },
    { name: 'analysis', path: '/analysis', waitFor: '.container' },
    { name: 'settings', path: '/settings', waitFor: '.container' }
];

// Helper function to wait for app to be ready
async function waitForApp(url, maxRetries = 30) {
    console.log(`Waiting for app at ${url}...`);
    for (let i = 0; i < maxRetries; i++) {
        try {
            const response = await fetch(url);
            if (response.ok) {
                console.log('App is ready!');
                return true;
            }
        } catch (e) {
            // App not ready yet
        }
        await new Promise(resolve => setTimeout(resolve, 2000));
    }
    throw new Error('App failed to start after 60 seconds');
}

// Helper function to take screenshot with retry
async function takeScreenshot(page, filepath, description) {
    console.log(`  Taking screenshot: ${description}`);
    try {
        await page.screenshot({ 
            path: filepath, 
            fullPage: false,
            clip: { x: 0, y: 0, width: VIEWPORT.width, height: VIEWPORT.height }
        });
        console.log(`  ✓ Screenshot saved: ${path.basename(filepath)}`);
        return true;
    } catch (error) {
        console.error(`  ✗ Failed to take screenshot: ${error.message}`);
        return false;
    }
}

// Helper function to navigate and capture page
async function capturePageScreenshot(page, pageConfig, screenshotPrefix, screenshotDir) {
    const { name, path: pagePath, waitFor } = pageConfig;
    const timestamp = Date.now();
    const filename = `${screenshotPrefix}_${name}_${timestamp}.png`;
    const filepath = path.join(screenshotDir, filename);
    
    console.log(`\nTesting ${name} page (${pagePath})...`);
    
    try {
        // Navigate to page
        await page.goto(`${APP_URL}${pagePath}`, { 
            waitUntil: 'networkidle',
            timeout: TIMEOUT 
        });
        
        // Wait for page to be fully loaded
        await page.waitForTimeout(2000);
        
        // Wait for specific element if provided
        if (waitFor) {
            try {
                await page.waitForSelector(waitFor, { timeout: 10000 });
            } catch (e) {
                console.log(`  Note: Selector '${waitFor}' not found, continuing...`);
            }
        }
        
        // Additional wait for any animations
        await page.waitForTimeout(1000);
        
        // Take screenshot
        await takeScreenshot(page, filepath, `${name} page`);
        
        // Special handling for duplicates page - capture additional elements
        if (name === 'duplicates') {
            console.log('  Capturing additional Duplicates page elements...');
            
            // Look for and capture any duplicate groups
            const duplicateGroups = await page.$$('.duplicate-group-card');
            if (duplicateGroups.length > 0) {
                console.log(`  Found ${duplicateGroups.length} duplicate groups`);
                
                // Scroll to ensure cards are visible
                await page.evaluate(() => window.scrollTo(0, 200));
                await page.waitForTimeout(500);
                
                const detailFilename = `${screenshotPrefix}_${name}_detail_${timestamp}.png`;
                const detailFilepath = path.join(screenshotDir, detailFilename);
                await takeScreenshot(page, detailFilepath, 'Duplicates page with cards');
            }
            
            // Check for buttons
            const buttons = await page.$$('.btn-danger-primary, .btn-secondary, button');
            console.log(`  Found ${buttons.length} buttons on page`);
        }
        
        return { success: true, filename };
    } catch (error) {
        console.error(`  ✗ Error testing ${name} page: ${error.message}`);
        return { success: false, error: error.message };
    }
}

// Main test function
async function runTests(branch, screenshotDir) {
    console.log(`\n${'='.repeat(60)}`);
    console.log(`Running QA Tests for branch: ${branch}`);
    console.log(`Screenshots will be saved to: ${screenshotDir}`);
    console.log(`${'='.repeat(60)}`);
    
    const browser = await chromium.launch({
        headless: true,
        args: ['--no-sandbox', '--disable-setuid-sandbox']
    });
    
    const context = await browser.newContext({
        viewport: VIEWPORT,
        ignoreHTTPSErrors: true
    });
    
    const page = await context.newPage();
    
    // Set longer default timeout
    page.setDefaultTimeout(TIMEOUT);
    
    const results = [];
    
    try {
        // Wait for app to be ready
        await waitForApp(APP_URL);
        
        // Test each page
        for (const pageConfig of PAGES_TO_TEST) {
            const result = await capturePageScreenshot(
                page, 
                pageConfig, 
                branch === 'main' ? 'before' : 'after',
                screenshotDir
            );
            results.push({
                page: pageConfig.name,
                ...result
            });
        }
        
        // Additional CSS validation for PR branch
        if (branch !== 'main') {
            console.log('\n--- CSS Validation ---');
            
            // Navigate to home page for CSS check
            await page.goto(APP_URL, { waitUntil: 'networkidle' });
            
            // Check if CSS files are loaded
            const cssFiles = await page.evaluate(() => {
                const stylesheets = Array.from(document.styleSheets);
                return stylesheets.map(sheet => {
                    try {
                        return {
                            href: sheet.href,
                            rules: sheet.cssRules ? sheet.cssRules.length : 0
                        };
                    } catch (e) {
                        return {
                            href: sheet.href,
                            error: 'Could not access rules'
                        };
                    }
                });
            });
            
            console.log('Loaded stylesheets:');
            cssFiles.forEach(css => {
                if (css.href) {
                    console.log(`  - ${css.href} (${css.rules || 0} rules)`);
                }
            });
            
            // Check for MyHelper UI styles
            const hasMyHelperStyles = await page.evaluate(() => {
                // Check for specific MyHelper UI classes
                const testClasses = [
                    '.btn-primary', '.btn-secondary', '.card', 
                    '.stat-card', '.section-header'
                ];
                
                for (const className of testClasses) {
                    const element = document.querySelector(className);
                    if (element) {
                        const styles = window.getComputedStyle(element);
                        if (styles.display !== 'none') {
                            return true;
                        }
                    }
                }
                
                // Also check if myhelper.min.css is loaded
                const links = Array.from(document.querySelectorAll('link[rel="stylesheet"]'));
                return links.some(link => link.href.includes('myhelper'));
            });
            
            console.log(`MyHelper UI styles detected: ${hasMyHelperStyles ? 'YES' : 'NO'}`);
            
            results.push({
                page: 'css-validation',
                success: true,
                cssLoaded: cssFiles.length > 0,
                myhelperStyles: hasMyHelperStyles
            });
        }
        
    } catch (error) {
        console.error(`Critical error during testing: ${error.message}`);
        results.push({
            page: 'error',
            success: false,
            error: error.message
        });
    } finally {
        await browser.close();
    }
    
    return results;
}

// Export for use in other scripts
module.exports = { runTests, PAGES_TO_TEST };

// Run if executed directly
if (require.main === module) {
    const branch = process.argv[2] || 'main';
    const screenshotType = branch === 'main' ? 'before' : 'after';
    const screenshotDir = path.join(SCREENSHOT_DIR, screenshotType);
    
    runTests(branch, screenshotDir)
        .then(results => {
            console.log('\n=== Test Results Summary ===');
            results.forEach(result => {
                const status = result.success ? '✓' : '✗';
                console.log(`${status} ${result.page}: ${result.success ? 'SUCCESS' : result.error}`);
            });
            
            // Save results to JSON
            const resultsFile = path.join(__dirname, '..', `test-results-${Date.now()}.json`);
            fs.writeFileSync(resultsFile, JSON.stringify(results, null, 2));
            console.log(`\nResults saved to: ${resultsFile}`);
        })
        .catch(error => {
            console.error('Test execution failed:', error);
            process.exit(1);
        });
}
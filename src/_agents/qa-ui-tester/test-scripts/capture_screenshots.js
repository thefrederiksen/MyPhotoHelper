const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

async function captureScreenshots(baseUrl, screenshotDir, prefix) {
    const browser = await chromium.launch({ 
        headless: true,
        args: ['--disable-gpu', '--no-sandbox', '--disable-setuid-sandbox']
    });
    
    const context = await browser.newContext({
        viewport: { width: 1920, height: 1080 }
    });
    
    const page = await context.newPage();
    
    // Set longer timeouts for slower pages
    page.setDefaultTimeout(30000);
    page.setDefaultNavigationTimeout(30000);
    
    const timestamp = Date.now();
    const screenshots = [];
    
    console.log(`Capturing screenshots for ${prefix} version...`);
    
    try {
        // 1. Home Page
        console.log('  - Navigating to Home page...');
        await page.goto(baseUrl, { waitUntil: 'networkidle' });
        await page.waitForTimeout(2000); // Wait for any animations
        const homePath = path.join(screenshotDir, `${prefix}_home_${timestamp}.png`);
        await page.screenshot({ path: homePath, fullPage: true });
        screenshots.push({ page: 'Home', path: homePath });
        console.log('    ✓ Home page captured');
        
        // 2. Duplicates Page (primary focus for CSS changes)
        console.log('  - Navigating to Duplicates page...');
        await page.goto(`${baseUrl}/duplicates`, { waitUntil: 'networkidle' });
        await page.waitForTimeout(3000); // Wait for content to load
        
        // Capture full page
        const duplicatesFullPath = path.join(screenshotDir, `${prefix}_duplicates_full_${timestamp}.png`);
        await page.screenshot({ path: duplicatesFullPath, fullPage: true });
        screenshots.push({ page: 'Duplicates Full', path: duplicatesFullPath });
        console.log('    ✓ Duplicates page (full) captured');
        
        // Capture specific UI components if they exist
        try {
            // Statistics cards
            const statsCard = await page.$('.stat-card-enhanced');
            if (statsCard) {
                const statsPath = path.join(screenshotDir, `${prefix}_duplicates_stats_${timestamp}.png`);
                await statsCard.screenshot({ path: statsPath });
                screenshots.push({ page: 'Statistics Cards', path: statsPath });
                console.log('    ✓ Statistics cards captured');
            }
            
            // Duplicate group card
            const groupCard = await page.$('.duplicate-group-card');
            if (groupCard) {
                const groupPath = path.join(screenshotDir, `${prefix}_duplicates_group_${timestamp}.png`);
                await groupCard.screenshot({ path: groupPath });
                screenshots.push({ page: 'Duplicate Group', path: groupPath });
                console.log('    ✓ Duplicate group card captured');
            }
            
            // Buttons
            const dangerBtn = await page.$('.btn-danger-primary');
            if (dangerBtn) {
                const btnPath = path.join(screenshotDir, `${prefix}_button_danger_${timestamp}.png`);
                await dangerBtn.screenshot({ path: btnPath });
                screenshots.push({ page: 'Danger Button', path: btnPath });
                console.log('    ✓ Danger button captured');
            }
        } catch (e) {
            console.log('    - Some UI components not found (may not have duplicates)');
        }
        
        // 3. Search Page
        console.log('  - Navigating to Search page...');
        await page.goto(`${baseUrl}/search`, { waitUntil: 'networkidle' });
        await page.waitForTimeout(2000);
        const searchPath = path.join(screenshotDir, `${prefix}_search_${timestamp}.png`);
        await page.screenshot({ path: searchPath, fullPage: true });
        screenshots.push({ page: 'Search', path: searchPath });
        console.log('    ✓ Search page captured');
        
        // 4. Settings Page
        console.log('  - Navigating to Settings page...');
        await page.goto(`${baseUrl}/settings`, { waitUntil: 'networkidle' });
        await page.waitForTimeout(2000);
        const settingsPath = path.join(screenshotDir, `${prefix}_settings_${timestamp}.png`);
        await page.screenshot({ path: settingsPath, fullPage: true });
        screenshots.push({ page: 'Settings', path: settingsPath });
        console.log('    ✓ Settings page captured');
        
        // 5. About Page
        console.log('  - Navigating to About page...');
        await page.goto(`${baseUrl}/about`, { waitUntil: 'networkidle' });
        await page.waitForTimeout(2000);
        const aboutPath = path.join(screenshotDir, `${prefix}_about_${timestamp}.png`);
        await page.screenshot({ path: aboutPath, fullPage: true });
        screenshots.push({ page: 'About', path: aboutPath });
        console.log('    ✓ About page captured');
        
        // Test button hover states (if buttons exist)
        console.log('  - Testing button hover states...');
        try {
            await page.goto(`${baseUrl}/duplicates`, { waitUntil: 'networkidle' });
            await page.waitForTimeout(2000);
            
            // Find and hover over buttons
            const buttons = await page.$$('button');
            for (let i = 0; i < Math.min(3, buttons.length); i++) {
                const button = buttons[i];
                const box = await button.boundingBox();
                if (box) {
                    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
                    await page.waitForTimeout(500);
                    const hoverPath = path.join(screenshotDir, `${prefix}_button_hover_${i}_${timestamp}.png`);
                    await button.screenshot({ path: hoverPath });
                    screenshots.push({ page: `Button Hover ${i}`, path: hoverPath });
                }
            }
            console.log('    ✓ Button hover states captured');
        } catch (e) {
            console.log('    - No buttons found for hover testing');
        }
        
    } catch (error) {
        console.error(`Error capturing screenshots: ${error.message}`);
        // Capture error screenshot
        const errorPath = path.join(screenshotDir, `${prefix}_error_${timestamp}.png`);
        await page.screenshot({ path: errorPath });
        screenshots.push({ page: 'Error', path: errorPath, error: error.message });
    } finally {
        await browser.close();
    }
    
    return screenshots;
}

module.exports = { captureScreenshots };

// Run if called directly
if (require.main === module) {
    const args = process.argv.slice(2);
    const url = args[0] || 'http://localhost:5113';
    const dir = args[1] || '.';
    const prefix = args[2] || 'screenshot';
    
    captureScreenshots(url, dir, prefix)
        .then(screenshots => {
            console.log('\nScreenshots captured:');
            screenshots.forEach(s => console.log(`  - ${s.page}: ${path.basename(s.path)}`));
        })
        .catch(err => {
            console.error('Failed to capture screenshots:', err);
            process.exit(1);
        });
}
const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

// Configuration
const CONFIG = {
  baseUrl: 'http://localhost:5113',
  viewport: { width: 1920, height: 1080 },
  screenshotDir: path.join(__dirname, 'screenshots', 'main'),
  timeout: 30000,
  navigationTimeout: 30000,
  actionTimeout: 10000
};

// Ensure screenshot directory exists
if (!fs.existsSync(CONFIG.screenshotDir)) {
  fs.mkdirSync(CONFIG.screenshotDir, { recursive: true });
}

async function delay(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

async function testMainBranch() {
  console.log('Starting UI Testing for Main Branch');
  console.log('=====================================');
  
  const browser = await chromium.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox']
  });

  const context = await browser.newContext({
    viewport: CONFIG.viewport,
    ignoreHTTPSErrors: true
  });

  const page = await context.newPage();
  page.setDefaultTimeout(CONFIG.timeout);
  page.setDefaultNavigationTimeout(CONFIG.navigationTimeout);

  try {
    // Test 1: Navigate to home page
    console.log('\nTest 1: Loading home page...');
    await page.goto(CONFIG.baseUrl, { waitUntil: 'networkidle' });
    await delay(2000); // Wait for full render
    await page.screenshot({ 
      path: path.join(CONFIG.screenshotDir, 'home_page.png'),
      fullPage: true 
    });
    console.log('✓ Home page loaded and screenshot captured');

    // Test 2: Navigate to Duplicates page
    console.log('\nTest 2: Navigating to Duplicates page...');
    
    // Try clicking the Duplicates link in the sidebar
    const duplicatesLink = await page.locator('a[href="/duplicates"]').first();
    if (await duplicatesLink.isVisible()) {
      await duplicatesLink.click();
    } else {
      // Direct navigation if link not found
      await page.goto(`${CONFIG.baseUrl}/duplicates`, { waitUntil: 'networkidle' });
    }
    
    await page.waitForLoadState('networkidle');
    await delay(3000); // Wait for data loading
    
    // Test 3: Capture main Duplicates page
    console.log('\nTest 3: Capturing Duplicates page...');
    await page.screenshot({ 
      path: path.join(CONFIG.screenshotDir, 'duplicates_main_page.png'),
      fullPage: true 
    });
    console.log('✓ Duplicates page screenshot captured');

    // Test 4: Capture statistics cards area
    console.log('\nTest 4: Capturing statistics area...');
    const statsSection = await page.locator('.grid').first();
    if (await statsSection.isVisible()) {
      await statsSection.screenshot({ 
        path: path.join(CONFIG.screenshotDir, 'duplicates_statistics.png')
      });
      console.log('✓ Statistics cards screenshot captured');
    } else {
      console.log('⚠ Statistics section not found');
    }

    // Test 5: Capture Delete All Duplicates button
    console.log('\nTest 5: Capturing Delete All button...');
    
    // Look for the Delete All Duplicates button
    const deleteAllButton = await page.locator('button:has-text("Delete All Duplicates")').first();
    if (await deleteAllButton.isVisible()) {
      // Capture button in normal state
      await deleteAllButton.screenshot({ 
        path: path.join(CONFIG.screenshotDir, 'delete_all_button_normal.png')
      });
      console.log('✓ Delete All button normal state captured');
      
      // Capture button hover state
      await deleteAllButton.hover();
      await delay(500);
      await deleteAllButton.screenshot({ 
        path: path.join(CONFIG.screenshotDir, 'delete_all_button_hover.png')
      });
      console.log('✓ Delete All button hover state captured');
      
      // Move mouse away to reset hover
      await page.mouse.move(0, 0);
      await delay(500);
    } else {
      console.log('⚠ Delete All Duplicates button not found');
    }

    // Test 6: Capture duplicate group cards
    console.log('\nTest 6: Capturing duplicate group cards...');
    
    // Look for duplicate group containers
    const duplicateGroups = await page.locator('.bg-white.rounded-lg.shadow').all();
    
    if (duplicateGroups.length > 0) {
      console.log(`Found ${duplicateGroups.length} duplicate groups`);
      
      // Capture first duplicate group if exists
      if (duplicateGroups.length >= 1) {
        await duplicateGroups[0].screenshot({ 
          path: path.join(CONFIG.screenshotDir, 'duplicate_group_1.png')
        });
        console.log('✓ First duplicate group captured');
      }
      
      // Capture second duplicate group if exists
      if (duplicateGroups.length >= 2) {
        await duplicateGroups[1].screenshot({ 
          path: path.join(CONFIG.screenshotDir, 'duplicate_group_2.png')
        });
        console.log('✓ Second duplicate group captured');
      }
    } else {
      console.log('⚠ No duplicate groups found - may need to scan for duplicates first');
    }

    // Test 7: Capture individual delete buttons
    console.log('\nTest 7: Capturing individual delete buttons...');
    
    const deleteButtons = await page.locator('button:has-text("Delete")').all();
    
    if (deleteButtons.length > 0) {
      console.log(`Found ${deleteButtons.length} individual delete buttons`);
      
      // Capture first delete button
      if (deleteButtons.length >= 1) {
        await deleteButtons[0].screenshot({ 
          path: path.join(CONFIG.screenshotDir, 'delete_button_normal.png')
        });
        
        // Hover state
        await deleteButtons[0].hover();
        await delay(500);
        await deleteButtons[0].screenshot({ 
          path: path.join(CONFIG.screenshotDir, 'delete_button_hover.png')
        });
        console.log('✓ Individual delete button states captured');
        
        // Reset hover
        await page.mouse.move(0, 0);
      }
    } else {
      console.log('⚠ No individual delete buttons found');
    }

    // Test 8: Capture page with scrolling for full content
    console.log('\nTest 8: Capturing full page content...');
    await page.evaluate(() => window.scrollTo(0, 0));
    await delay(1000);
    
    // Capture viewport screenshot
    await page.screenshot({ 
      path: path.join(CONFIG.screenshotDir, 'duplicates_viewport.png'),
      fullPage: false 
    });
    console.log('✓ Viewport screenshot captured');
    
    // Capture full page
    await page.screenshot({ 
      path: path.join(CONFIG.screenshotDir, 'duplicates_fullpage.png'),
      fullPage: true 
    });
    console.log('✓ Full page screenshot captured');

    console.log('\n=====================================');
    console.log('Main Branch Testing Complete!');
    console.log(`Screenshots saved to: ${CONFIG.screenshotDir}`);
    console.log('=====================================');

  } catch (error) {
    console.error('\n❌ Test failed:', error.message);
    
    // Capture error screenshot
    await page.screenshot({ 
      path: path.join(CONFIG.screenshotDir, 'error_screenshot.png'),
      fullPage: true 
    });
    console.log('Error screenshot saved');
    
    throw error;
  } finally {
    await browser.close();
  }
}

// Run the tests
testMainBranch()
  .then(() => {
    console.log('\n✅ All tests completed successfully');
    process.exit(0);
  })
  .catch((error) => {
    console.error('\n❌ Testing failed:', error);
    process.exit(1);
  });
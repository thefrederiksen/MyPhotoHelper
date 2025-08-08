const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

// Configuration
const CONFIG = {
  baseUrl: 'http://localhost:5113',
  viewport: { width: 1920, height: 1080 },
  screenshotDir: path.join(__dirname, 'screenshots', 'pr'),
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

async function testPRBranch() {
  console.log('Starting UI Testing for PR Branch (fix-delete-duplicates-button-ui)');
  console.log('==========================================================');
  
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

    // Test 5: Capture Delete All Duplicates button (NEW DESIGN)
    console.log('\nTest 5: Capturing Delete All button (NEW DESIGN)...');
    
    // Look for the Delete All Duplicates button with new styling
    const deleteAllButton = await page.locator('button.btn-danger-primary').first();
    if (await deleteAllButton.isVisible()) {
      // Capture button in normal state
      await deleteAllButton.screenshot({ 
        path: path.join(CONFIG.screenshotDir, 'delete_all_button_normal.png')
      });
      console.log('✓ Delete All button normal state captured (NEW DESIGN)');
      
      // Capture button hover state
      await deleteAllButton.hover();
      await delay(500);
      await deleteAllButton.screenshot({ 
        path: path.join(CONFIG.screenshotDir, 'delete_all_button_hover.png')
      });
      console.log('✓ Delete All button hover state captured (NEW DESIGN)');
      
      // Move mouse away to reset hover
      await page.mouse.move(0, 0);
      await delay(500);
    } else {
      console.log('⚠ Delete All Duplicates button not found');
    }

    // Test 6: Capture duplicate group cards with new styling
    console.log('\nTest 6: Capturing duplicate group cards (NEW DESIGN)...');
    
    // Look for duplicate group containers with new class
    const duplicateGroups = await page.locator('.duplicate-group-card').all();
    
    if (duplicateGroups.length > 0) {
      console.log(`Found ${duplicateGroups.length} duplicate groups`);
      
      // Capture first duplicate group if exists
      if (duplicateGroups.length >= 1) {
        await duplicateGroups[0].screenshot({ 
          path: path.join(CONFIG.screenshotDir, 'duplicate_group_1.png')
        });
        console.log('✓ First duplicate group captured (NEW DESIGN)');
      }
      
      // Capture second duplicate group if exists
      if (duplicateGroups.length >= 2) {
        await duplicateGroups[1].screenshot({ 
          path: path.join(CONFIG.screenshotDir, 'duplicate_group_2.png')
        });
        console.log('✓ Second duplicate group captured (NEW DESIGN)');
      }
    } else {
      console.log('⚠ No duplicate groups found - may need to scan for duplicates first');
    }

    // Test 7: Capture individual delete buttons with new styling
    console.log('\nTest 7: Capturing individual delete buttons (NEW DESIGN)...');
    
    // Look for the new Delete Group buttons
    const deleteGroupButtons = await page.locator('button.btn-danger-small').all();
    
    if (deleteGroupButtons.length > 0) {
      console.log(`Found ${deleteGroupButtons.length} delete group buttons`);
      
      // Capture first delete button
      if (deleteGroupButtons.length >= 1) {
        await deleteGroupButtons[0].screenshot({ 
          path: path.join(CONFIG.screenshotDir, 'delete_group_button_normal.png')
        });
        
        // Hover state
        await deleteGroupButtons[0].hover();
        await delay(500);
        await deleteGroupButtons[0].screenshot({ 
          path: path.join(CONFIG.screenshotDir, 'delete_group_button_hover.png')
        });
        console.log('✓ Delete Group button states captured (NEW DESIGN)');
        
        // Reset hover
        await page.mouse.move(0, 0);
      }
    }
    
    // Also capture individual file delete buttons
    const deleteFileButtons = await page.locator('button.delete-single-btn').all();
    
    if (deleteFileButtons.length > 0) {
      console.log(`Found ${deleteFileButtons.length} individual file delete buttons`);
      
      // Capture first delete file button
      if (deleteFileButtons.length >= 1) {
        await deleteFileButtons[0].screenshot({ 
          path: path.join(CONFIG.screenshotDir, 'delete_file_button_normal.png')
        });
        
        // Hover state
        await deleteFileButtons[0].hover();
        await delay(500);
        await deleteFileButtons[0].screenshot({ 
          path: path.join(CONFIG.screenshotDir, 'delete_file_button_hover.png')
        });
        console.log('✓ Delete File button states captured (NEW DESIGN)');
        
        // Reset hover
        await page.mouse.move(0, 0);
      }
    }

    // Test 8: Test Delete All confirmation dialog
    console.log('\nTest 8: Testing Delete All confirmation dialog...');
    
    if (await deleteAllButton.isVisible()) {
      // Click Delete All to show confirmation
      await deleteAllButton.click();
      await delay(1000);
      
      // Check if confirmation dialog appeared
      const confirmDialog = await page.locator('.bg-red-50.border-red-300').first();
      if (await confirmDialog.isVisible()) {
        await confirmDialog.screenshot({ 
          path: path.join(CONFIG.screenshotDir, 'delete_all_confirmation.png')
        });
        console.log('✓ Delete All confirmation dialog captured');
        
        // Capture the confirm button
        const confirmButton = await page.locator('button.btn-danger-confirm').first();
        if (await confirmButton.isVisible()) {
          await confirmButton.screenshot({ 
            path: path.join(CONFIG.screenshotDir, 'confirm_delete_button.png')
          });
          console.log('✓ Confirm delete button captured');
        }
        
        // Click cancel to close dialog
        const cancelButton = await page.locator('button.btn-secondary').last();
        if (await cancelButton.isVisible()) {
          await cancelButton.click();
          await delay(500);
        }
      }
    }

    // Test 9: Capture enhanced statistics cards
    console.log('\nTest 9: Capturing enhanced statistics cards...');
    
    const statCards = await page.locator('.stat-card-enhanced').all();
    if (statCards.length > 0) {
      console.log(`Found ${statCards.length} enhanced statistics cards`);
      
      for (let i = 0; i < Math.min(statCards.length, 3); i++) {
        await statCards[i].screenshot({ 
          path: path.join(CONFIG.screenshotDir, `stat_card_${i + 1}.png`)
        });
      }
      console.log('✓ Enhanced statistics cards captured');
    }

    // Test 10: Capture full page with scrolling for full content
    console.log('\nTest 10: Capturing full page content...');
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

    console.log('\n==========================================================');
    console.log('PR Branch Testing Complete!');
    console.log(`Screenshots saved to: ${CONFIG.screenshotDir}`);
    console.log('==========================================================');

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
testPRBranch()
  .then(() => {
    console.log('\n✅ All PR branch tests completed successfully');
    process.exit(0);
  })
  .catch((error) => {
    console.error('\n❌ PR branch testing failed:', error);
    process.exit(1);
  });
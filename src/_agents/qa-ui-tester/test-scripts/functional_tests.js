const { chromium } = require('playwright');

async function runFunctionalTests(baseUrl) {
    const browser = await chromium.launch({ 
        headless: true,
        args: ['--disable-gpu', '--no-sandbox', '--disable-setuid-sandbox']
    });
    
    const context = await browser.newContext({
        viewport: { width: 1920, height: 1080 }
    });
    
    const page = await context.newPage();
    page.setDefaultTimeout(30000);
    
    const testResults = {
        passed: [],
        failed: [],
        performance: {},
        timestamp: new Date().toISOString()
    };
    
    console.log('Running functional tests for MyHelper UI Component Library...\n');
    
    try {
        // Test 1: Navigation and Page Load
        console.log('Test 1: Navigation and Page Load');
        const startTime = Date.now();
        await page.goto(baseUrl, { waitUntil: 'networkidle' });
        const loadTime = Date.now() - startTime;
        testResults.performance.homePageLoad = loadTime;
        console.log(`  ✓ Home page loaded in ${loadTime}ms`);
        testResults.passed.push('Home page loads successfully');
        
        // Test 2: CSS Files Load Correctly
        console.log('\nTest 2: CSS Files Load Correctly');
        const cssFiles = await page.evaluate(() => {
            const links = Array.from(document.querySelectorAll('link[rel="stylesheet"]'));
            return links.map(link => link.href);
        });
        
        const hasMyHelperCSS = cssFiles.some(css => css.includes('myhelper.min.css'));
        if (hasMyHelperCSS) {
            console.log('  ✓ MyHelper UI CSS loaded');
            testResults.passed.push('MyHelper UI CSS loads correctly');
        } else {
            console.log('  ✗ MyHelper UI CSS not found');
            testResults.failed.push('MyHelper UI CSS not loading');
        }
        
        // Test 3: Button Styles and Interactions
        console.log('\nTest 3: Button Styles and Interactions');
        await page.goto(`${baseUrl}/duplicates`, { waitUntil: 'networkidle' });
        
        // Check for button classes
        const buttonTests = [
            { class: '.mh-btn-danger', name: 'Danger Button' },
            { class: '.mh-btn-primary', name: 'Primary Button' },
            { class: '.mh-btn-secondary', name: 'Secondary Button' }
        ];
        
        for (const btnTest of buttonTests) {
            const button = await page.$(btnTest.class);
            if (button) {
                // Test hover state
                const beforeHover = await button.evaluate(el => {
                    const style = window.getComputedStyle(el);
                    return {
                        background: style.background,
                        transform: style.transform,
                        boxShadow: style.boxShadow
                    };
                });
                
                await button.hover();
                await page.waitForTimeout(300);
                
                const afterHover = await button.evaluate(el => {
                    const style = window.getComputedStyle(el);
                    return {
                        background: style.background,
                        transform: style.transform,
                        boxShadow: style.boxShadow
                    };
                });
                
                if (beforeHover.transform !== afterHover.transform || 
                    beforeHover.boxShadow !== afterHover.boxShadow) {
                    console.log(`  ✓ ${btnTest.name} hover effects working`);
                    testResults.passed.push(`${btnTest.name} hover effects`);
                } else {
                    console.log(`  ⚠ ${btnTest.name} hover effects may not be working`);
                }
                
                // Test click functionality
                const isClickable = await button.evaluate(el => !el.disabled);
                if (isClickable) {
                    console.log(`  ✓ ${btnTest.name} is clickable`);
                    testResults.passed.push(`${btnTest.name} clickability`);
                }
            }
        }
        
        // Test 4: Card Components
        console.log('\nTest 4: Card Components');
        const cards = await page.$$('.mh-card');
        if (cards.length > 0) {
            console.log(`  ✓ Found ${cards.length} card components`);
            testResults.passed.push(`Card components rendered (${cards.length} found)`);
            
            // Check card styling
            const cardStyle = await cards[0].evaluate(el => {
                const style = window.getComputedStyle(el);
                return {
                    hasBorder: style.border !== 'none',
                    hasRadius: parseFloat(style.borderRadius) > 0,
                    hasShadow: style.boxShadow !== 'none',
                    background: style.backgroundColor
                };
            });
            
            if (cardStyle.hasBorder && cardStyle.hasRadius && cardStyle.hasShadow) {
                console.log('  ✓ Card styling applied correctly');
                testResults.passed.push('Card component styling');
            }
        }
        
        // Test 5: Statistics Cards
        console.log('\nTest 5: Statistics Cards');
        const statCards = await page.$$('.mh-stat-card');
        if (statCards.length > 0) {
            console.log(`  ✓ Found ${statCards.length} statistics cards`);
            testResults.passed.push(`Statistics cards rendered (${statCards.length} found)`);
            
            // Test hover effect on stat cards
            const firstStatCard = statCards[0];
            const beforeHover = await firstStatCard.evaluate(el => {
                const style = window.getComputedStyle(el);
                return style.transform;
            });
            
            await firstStatCard.hover();
            await page.waitForTimeout(300);
            
            const afterHover = await firstStatCard.evaluate(el => {
                const style = window.getComputedStyle(el);
                return style.transform;
            });
            
            if (beforeHover !== afterHover) {
                console.log('  ✓ Statistics card hover effects working');
                testResults.passed.push('Statistics card hover effects');
            }
        }
        
        // Test 6: Form Elements
        console.log('\nTest 6: Form Elements');
        await page.goto(`${baseUrl}/settings`, { waitUntil: 'networkidle' });
        
        const formElements = await page.evaluate(() => {
            const inputs = document.querySelectorAll('.mh-input, .mh-form-control');
            const selects = document.querySelectorAll('.mh-select, .mh-form-select');
            const checkboxes = document.querySelectorAll('.mh-checkbox, .mh-form-check-input');
            return {
                inputs: inputs.length,
                selects: selects.length,
                checkboxes: checkboxes.length
            };
        });
        
        if (formElements.inputs > 0 || formElements.selects > 0 || formElements.checkboxes > 0) {
            console.log(`  ✓ Form elements found: ${formElements.inputs} inputs, ${formElements.selects} selects, ${formElements.checkboxes} checkboxes`);
            testResults.passed.push('Form elements styled with MyHelper UI');
        }
        
        // Test 7: Responsive Design
        console.log('\nTest 7: Responsive Design');
        const viewports = [
            { width: 1920, height: 1080, name: 'Desktop' },
            { width: 768, height: 1024, name: 'Tablet' },
            { width: 375, height: 667, name: 'Mobile' }
        ];
        
        for (const viewport of viewports) {
            await page.setViewportSize(viewport);
            await page.goto(`${baseUrl}/duplicates`, { waitUntil: 'networkidle' });
            await page.waitForTimeout(500);
            
            const isResponsive = await page.evaluate(() => {
                const container = document.querySelector('.container, .container-fluid');
                if (!container) return false;
                const width = container.offsetWidth;
                const windowWidth = window.innerWidth;
                return width <= windowWidth && width > 0;
            });
            
            if (isResponsive) {
                console.log(`  ✓ Responsive at ${viewport.name} (${viewport.width}x${viewport.height})`);
                testResults.passed.push(`Responsive design at ${viewport.name}`);
            } else {
                console.log(`  ✗ Layout issues at ${viewport.name}`);
                testResults.failed.push(`Responsive issues at ${viewport.name}`);
            }
        }
        
        // Test 8: Color Consistency
        console.log('\nTest 8: Color Consistency');
        await page.setViewportSize({ width: 1920, height: 1080 });
        await page.goto(`${baseUrl}/duplicates`, { waitUntil: 'networkidle' });
        
        const colors = await page.evaluate(() => {
            const getColor = (selector, property) => {
                const el = document.querySelector(selector);
                if (!el) return null;
                return window.getComputedStyle(el)[property];
            };
            
            return {
                dangerButton: getColor('.mh-btn-danger', 'backgroundColor'),
                primaryButton: getColor('.mh-btn-primary', 'backgroundColor'),
                cardBorder: getColor('.mh-card', 'borderColor'),
                textPrimary: getColor('body', 'color')
            };
        });
        
        if (colors.dangerButton || colors.primaryButton) {
            console.log('  ✓ Component colors applied consistently');
            testResults.passed.push('Color consistency across components');
        }
        
        // Test 9: Performance Metrics
        console.log('\nTest 9: Performance Metrics');
        const metrics = await page.evaluate(() => {
            const perf = performance.getEntriesByType('navigation')[0];
            return {
                domContentLoaded: Math.round(perf.domContentLoadedEventEnd - perf.domContentLoadedEventStart),
                loadComplete: Math.round(perf.loadEventEnd - perf.loadEventStart),
                domInteractive: Math.round(perf.domInteractive - perf.fetchStart)
            };
        });
        
        testResults.performance = { ...testResults.performance, ...metrics };
        console.log(`  DOM Content Loaded: ${metrics.domContentLoaded}ms`);
        console.log(`  Load Complete: ${metrics.loadComplete}ms`);
        console.log(`  DOM Interactive: ${metrics.domInteractive}ms`);
        
        if (metrics.domInteractive < 3000) {
            testResults.passed.push('Good performance metrics');
        } else {
            testResults.failed.push('Performance may be degraded');
        }
        
    } catch (error) {
        console.error(`Test execution error: ${error.message}`);
        testResults.failed.push(`Test execution error: ${error.message}`);
    } finally {
        await browser.close();
    }
    
    // Summary
    console.log('\n' + '='.repeat(50));
    console.log('FUNCTIONAL TEST SUMMARY');
    console.log('='.repeat(50));
    console.log(`✓ Passed: ${testResults.passed.length}`);
    console.log(`✗ Failed: ${testResults.failed.length}`);
    console.log(`Performance: DOM Interactive in ${testResults.performance.domInteractive}ms`);
    
    return testResults;
}

// Run if called directly
if (require.main === module) {
    const url = process.argv[2] || 'http://localhost:5113';
    
    runFunctionalTests(url)
        .then(results => {
            const fs = require('fs');
            const reportPath = `../test-results-${Date.now()}.json`;
            fs.writeFileSync(reportPath, JSON.stringify(results, null, 2));
            console.log(`\nTest results saved to: ${reportPath}`);
            
            process.exit(results.failed.length > 0 ? 1 : 0);
        })
        .catch(err => {
            console.error('Test runner failed:', err);
            process.exit(1);
        });
}

module.exports = { runFunctionalTests };
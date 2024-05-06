A CLI program that interacts with a specific website to efficiently and responsibly download images in batch.

## Features
1. **Automatic Retry Logic**: It includes an automatic retry mechanism for both page loading and image downloading, enhancing the reliability of the scraper.
2. **Lazy loading handling**: The scraper is adept at navigating through web pages that implement lazy loading. It employs a robust and efficient logic for scrolling to the bottom of a page and waiting for all images to load.
3. **Concurrent Downloads**: The scraper supports downloading multiple images concurrently, with a limit to prevent overloading servers or violating rate limits.
4. **Spinner and Progress Bar**: The scraper includes a spinner and progress bar for better user experience and to provide visual feedback during the scraping process.
5. **Block Unnecessary Requests**: The scraper is designed to block unnecessary requests, improving efficiency and speed.
6. **Logger Integration**: The scraper uses `LogDebug` for debugging of key parts and `LogWarning` for tracking potential issues like retries.

# BufferingHandler Has No Test Coverage

> Status: open.

## What

`CognitoUserPoolClient.BufferingHandler` (`blazwiz.Portal.Application/Services/CognitoUserPoolClient.cs`) is not
covered by unit tests. It is a private nested `DelegatingHandler` that reads each AWS SDK response body into memory
asynchronously, because the SDK reads responses synchronously and the browser does not allow that.

The rest of `CognitoUserPoolClient`, including the `BrowserHttpClientFactory` that wraps the handler, is covered.

## Why it is not covered

The handler is created inside a static field of `BrowserHttpClientFactory` around a real `HttpClientHandler`, so a
test cannot swap the inner handler for a fake. The only way to exercise it as is would be to send a real HTTP request.

## Options

1. **Test it as is**: send a request through `CreateProvider(...).Config.HttpClientFactory.CreateHttpClient(...)`
   to a local `HttpListener` and check the body was buffered. No production change, but it is a slower, network bound
   test in a unit test project.
2. **Make it injectable**: move the handler out to its own class (e.g. in `Services/`), so it can be constructed in a
   test with a fake inner handler, and let the factory take it as a dependency. Fits the "injectable services, no
   static helpers" approach, but changes how `CreateProvider` builds the client.

Option 2 is preferred.

## Risk while open

Low. The handler is a few lines, and it is exercised whenever the portal talks to Cognito, so a break would show up
straight away when signing in.

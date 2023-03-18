﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.CoreSkills;
using Microsoft.SemanticKernel.KernelExtensions;
using Microsoft.SemanticKernel.Reliability;
using Reliability;
using RepoUtils;

// ReSharper disable once InconsistentNaming
public static class Example08_RetryHandler
{
    public static async Task RunAsync()
    {
        var kernel = InitializeKernel();
        var retryHandlerFactory = new RetryThreeTimesWithBackoffFactory();
        ConsoleLogger.Log.LogInformation("============================== RetryThreeTimesWithBackoff ==============================");
        await RunRetryPolicyAsync(kernel, retryHandlerFactory);

        ConsoleLogger.Log.LogInformation("========================= RetryThreeTimesWithRetryAfterBackoff =========================");
        await RunRetryPolicyBuilderAsync(typeof(RetryThreeTimesWithRetryAfterBackoffFactory));

        ConsoleLogger.Log.LogInformation("==================================== NoRetryPolicy =====================================");
        await RunRetryPolicyBuilderAsync(typeof(NullHttpRetryHandlerFactory));

        ConsoleLogger.Log.LogInformation("=============================== DefaultHttpRetryHandler ================================");
        await RunRetryHandlerConfigAsync(new HttpRetryConfig() { MaxRetryCount = 3, UseExponentialBackoff = true });

        ConsoleLogger.Log.LogInformation("======= DefaultHttpRetryConfig [MaxRetryCount = 3, UseExponentialBackoff = true] =======");
        await RunRetryHandlerConfigAsync(new HttpRetryConfig() { MaxRetryCount = 3, UseExponentialBackoff = true });
    }

    private static async Task RunRetryHandlerConfigAsync(HttpRetryConfig? config = null)
    {
        var kernelBuilder = Kernel.Builder.WithLogger(ConsoleLogger.Log);
        if (config != null)
        {
            kernelBuilder = kernelBuilder.Configure(c => c.SetDefaultHttpRetryConfig(config));
        }

        // Add 401 to the list of retryable status codes
        // Typically 401 would not be something we retry but for demonstration
        // purposes we are doing so as it's easy to trigger when using an invalid key.
        kernelBuilder = kernelBuilder.Configure(c => c.DefaultHttpRetryConfig.RetryableStatusCodes.Add(System.Net.HttpStatusCode.Unauthorized));

        // OpenAI settings - you can set the OPENAI_API_KEY to an invalid value to see the retry policy in play
        kernelBuilder = kernelBuilder.Configure(c => c.AddOpenAICompletionBackend("text-davinci-003", "text-davinci-003", "BAD_KEY"));

        var kernel = kernelBuilder.Build();

        await ImportAndExecuteSkillAsync(kernel);
    }

    private static IKernel InitializeKernel()
    {
        var kernel = Kernel.Builder.WithLogger(ConsoleLogger.Log).Build();
        // OpenAI settings - you can set the OPENAI_API_KEY to an invalid value to see the retry policy in play
        kernel.Config.AddOpenAICompletionBackend("text-davinci-003", "text-davinci-003", "BAD_KEY");

        return kernel;
    }

    private static async Task RunRetryPolicyAsync(IKernel kernel, IDelegatingHandlerFactory retryHandlerFactory)
    {
        kernel.Config.SetHttpRetryHandlerFactory(retryHandlerFactory);
        await ImportAndExecuteSkillAsync(kernel);
    }

    private static async Task RunRetryPolicyBuilderAsync(Type retryHandlerFactoryType)
    {
        var kernelBuilder = Kernel.Builder.WithLogger(ConsoleLogger.Log)
            .WithRetryHandlerFactory((Activator.CreateInstance(retryHandlerFactoryType) as IDelegatingHandlerFactory)!);

        // OpenAI settings - you can set the OPENAI_API_KEY to an invalid value to see the retry policy in play
        kernelBuilder = kernelBuilder.Configure(c => c.AddOpenAICompletionBackend("text-davinci-003", "text-davinci-003", "BAD_KEY"));

        var kernel = kernelBuilder.Build();

        await ImportAndExecuteSkillAsync(kernel);
    }

    private static async Task ImportAndExecuteSkillAsync(IKernel kernel)
    {
        // Load semantic skill defined with prompt templates
        string folder = RepoFiles.SampleSkillsPath();

        kernel.ImportSkill(new TimeSkill(), "time");

        var qaSkill = kernel.ImportSemanticSkillFromDirectory(
            folder,
            "QASkill");

        var question = "How popular is Polly library?";

        ConsoleLogger.Log.LogInformation("Question: {0}", question);
        // To see the retry policy in play, you can set the OPENAI_API_KEY to an invalid value
        var answer = await kernel.RunAsync(question, qaSkill["Question"]);
        ConsoleLogger.Log.LogInformation("Answer: {0}", answer);
    }
}

/* Output:
info: object[0]
      ============================== RetryThreeTimesWithBackoff ==============================
info: object[0]
      Question: How popular is Polly library?
warn: object[0]
      Error executing action [attempt 1 of 3], pausing 2000ms. Outcome: Unauthorized
warn: object[0]
      Error executing action [attempt 2 of 3], pausing 4000ms. Outcome: Unauthorized
warn: object[0]
      Error executing action [attempt 3 of 3], pausing 8000ms. Outcome: Unauthorized
fail: object[0]
      Function call fail during pipeline step 0: QASkill.Question
info: object[0]
      Answer: Error: AccessDenied: The request is not authorized, HTTP status: Unauthorized
info: object[0]
      ========================= RetryThreeTimesWithRetryAfterBackoff =========================
info: object[0]
      Question: How popular is Polly library?
warn: object[0]
      Error executing action [attempt 1 of 3], pausing 2000ms. Outcome: Unauthorized
warn: object[0]
      Error executing action [attempt 2 of 3], pausing 2000ms. Outcome: Unauthorized
warn: object[0]
      Error executing action [attempt 3 of 3], pausing 2000ms. Outcome: Unauthorized
fail: object[0]
      Function call fail during pipeline step 0: QASkill.Question
info: object[0]
      Answer: Error: AccessDenied: The request is not authorized, HTTP status: Unauthorized
info: object[0]
      ==================================== NoRetryPolicy =====================================
info: object[0]
      Question: How popular is Polly library?
fail: object[0]
      Function call fail during pipeline step 0: QASkill.Question
info: object[0]
      Answer: Error: AccessDenied: The request is not authorized, HTTP status: Unauthorized
info: object[0]
      =============================== DefaultHttpRetryHandler ================================
info: object[0]
      Question: How popular is Polly library?
warn: object[0]
      Error executing action [attempt 1 of 3]. Reason: Unauthorized. Will retry after 2000ms
warn: object[0]
      Error executing action [attempt 2 of 3]. Reason: Unauthorized. Will retry after 4000ms
warn: object[0]
      Error executing action [attempt 3 of 3]. Reason: Unauthorized. Will retry after 8000ms
fail: object[0]
      Error executing request, max retry count reached. Reason: Unauthorized
fail: object[0]
      Function call fail during pipeline step 0: QASkill.Question
info: object[0]
      Answer: Error: AccessDenied: The request is not authorized, HTTP status: Unauthorized
info: object[0]
      ======= DefaultHttpRetryConfig [MaxRetryCount = 3, UseExponentialBackoff = true] =======
info: object[0]
      Question: How popular is Polly library?
warn: object[0]
      Error executing action [attempt 1 of 3]. Reason: Unauthorized. Will retry after 2000ms
warn: object[0]
      Error executing action [attempt 2 of 3]. Reason: Unauthorized. Will retry after 4000ms
warn: object[0]
      Error executing action [attempt 3 of 3]. Reason: Unauthorized. Will retry after 8000ms
fail: object[0]
      Error executing request, max retry count reached. Reason: Unauthorized
fail: object[0]
      Function call fail during pipeline step 0: QASkill.Question
info: object[0]
      Answer: Error: AccessDenied: The request is not authorized, HTTP status: Unauthorized
*/
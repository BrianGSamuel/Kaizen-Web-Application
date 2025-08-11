// Global using statements for the Kaizen Web Application
global using System;
global using System.Collections.Generic;
global using System.ComponentModel.DataAnnotations;
global using System.ComponentModel.DataAnnotations.Schema;
global using System.Diagnostics;
global using System.IO;
global using System.Linq;
global using System.Security.Claims;
global using System.Threading.Tasks;

// ASP.NET Core
global using Microsoft.AspNetCore.Authentication;
global using Microsoft.AspNetCore.Authentication.Cookies;
global using Microsoft.AspNetCore.Authorization;
global using Microsoft.AspNetCore.Hosting;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.Mvc.Rendering;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;

// Application namespaces
global using KaizenWebApp.Data;
global using KaizenWebApp.Models;
global using KaizenWebApp.ViewModels;
global using KaizenWebApp.Services;
global using KaizenWebApp.Configuration;
global using KaizenWebApp.Exceptions;
global using KaizenWebApp.Enums;
global using KaizenWebApp.Extensions;
global using KaizenWebApp.Middleware;

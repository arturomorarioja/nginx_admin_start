# NGINX Administration - start

Starting repo for an NGINX administration exercise.

It contains a Docker container with three applications related to an online training platform and a basic NGINX installation.

All programs run inside the same Linux container:

| Program | Local listener |
|---|---|
| NGINX | Port 80 |
| Learning application | `127.0.0.1:7001` |
| Certificate service | `127.0.0.1:7002` |
| Staff application | `127.0.0.1:7003` |

The application services start automatically alongside NGINX. The public site is in `sites/public/`, and service source code is in `applications/`.

The exercise consists in:

1. Configuring two virtual hosts:

    | Hostname | Website |
    |:---|:---|
    | `learn.local` | Public website and learning application |
    | `staff.learn.local` | Staff administration application |

2. Configuring reverse proxying:

    | Domain | URL path | Required behavior |
    |---|---|---|
    | `learn.local` | `/` | Serve the public website using NGINX |
    | `learn.local` | `/courses/` | Forward to the learning application, port 7001 |
    | `learn.local` | `/account/` | Forward to the learning application, port 7001 |
    | `learn.local` | `/certificates/` | Forward to the certificate service, port 7002 |
    | `staff.learn.local` | `/` | Forward to the staff application, port 7003 |

3. Serving static resources:
    - `/assets/` — Stylesheets and JavaScript, such as `/assets/site.css`
    - `/downloads/` — Public course information, such as `/downloads/course-guide.txt`

4. Protecting staff resources
    - `/settings/` must be password authenticated
    - `/internal` must be accessed only from the IP `127.0.0.1`

5. Configuring logging  
    Separate for the two virtual hosts:

    | Virtual host | Access log | Error log |
    |---|---|---|
    | `learn.local` | `/var/log/nginx/learn.access.log` | `/var/log/nginx/learn.error.log` |
    | `staff.learn.local` | `/var/log/nginx/staff.access.log` | `/var/log/nginx/staff.error.log` |

6. Configuring custom error pages  
    The error pages are in `/srv/www/errors/`:

    - `403.html` — Access denied.
    - `404.html` — Resource not found.
    - `502.html` — Backend unavailable.

## Start

```sh
docker compose up -d --build
```

The web server should be available at `http://localhost:8080/`.

## Tools
NGINX / .NET/ASP.NET Core 10.0 / Water.css / HTML5

## Author
ChatGPT 5.6-Sol, prompted by Arturo Mora-Rioja
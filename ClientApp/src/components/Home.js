import React, { Component } from 'react';

export class Home extends Component {
    static displayName = Home.name;

    static uploadFiles(event) {
        const data = new FormData();
        for (let i = 0; i < event.target.files.length; i++) {
            data.append('files', event.target.files[i]);
        };

        fetch('/Files', {
            method: 'POST',
            body: data
        });
    }

    render() {
        return (
            <div>
                <input type="file" onChange={Home.uploadFiles} multiple/>
            </div>
        );
    }
}

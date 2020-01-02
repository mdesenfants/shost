import React, { Component } from 'react';
import ImageGallery from 'react-image-gallery';
import "react-image-gallery/styles/css/image-gallery.css";

export class FetchData extends Component {
    static displayName = FetchData.name;

    constructor(props) {
        super(props);
        this.state = { forecasts: [], loading: true };
    }

    componentDidMount() {
        this.populateWeatherData();
    }

    static renderForecastsTable(forecasts) {
        //return (
        //    <div className="gallery">{forecasts.map(forecast =>
        //        <a key={forecast} href={forecast} className="profile">
        //            <img src={forecast} alt={forecast} className="profile" />
        //        </a>)}
        //    </div >
        //);

        return <ImageGallery items={forecasts} thumbnailPosition="left" />;
    }

    render() {
        let contents = this.state.loading
            ? <p><em>Loading...</em></p>
            : FetchData.renderForecastsTable(this.state.forecasts);

        return (
            <div>
                {contents}
            </div>
        );
    }

    shuffle(data) {
        let length = data.length;
        for (let i = length - 1; i > 0; i--) {
            let min = 0;
            let max = i;
            let grab = Math.floor(Math.random() * (max - min)) + min;

            let temp = data[i];
            data[i] = data[grab];
            data[grab] = temp;
        }

        return data;
    }

    async populateWeatherData() {
        const response = await fetch('files');
        const data = await response.json();
        const shuffled = this.shuffle(data).map(x => ({ original: x, thumbnail: x }));
        this.setState({ forecasts: shuffled, loading: false });
    }
}
